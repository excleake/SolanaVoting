use anchor_lang::prelude::*;

/// Program ID of the smart contract.
/// Must match the programId used in the C# client.
declare_id!("31RBt6nsdi6tEbKVffYi8CbT8HeLYQgdGyZo8J8uyP6k");

/// Main program module.
/// Contains all instructions (entrypoints) callable by clients.
#[program]
pub mod voting {
    use super::*;

    /// Initializes a new voting.
    ///
    /// Creates a VotingAccount (PDA) for a specific company and voting.
    /// Stores the question, answer options, and initializes vote counters.
    ///
    /// Parameters:
    /// - company_id: company identifier
    /// - voting_id: voting identifier
    /// - question: voting question text
    /// - options: list of answer options (2 or 3)
    pub fn initialize_voting(
        ctx: Context<InitializeVoting>,
        company_id: u64,
        voting_id: u64,
        question: String,
        options: Vec<String>,
    ) -> Result<()> {
        // Validate number of options
        require!(
            options.len() >= 2 && options.len() <= 3,
            VotingError::InvalidOptionsCount
        );

        let voting_account = &mut ctx.accounts.voting;

        // Store voting metadata
        voting_account.company_id = company_id;
        voting_account.voting_id = voting_id;
        voting_account.question = question;
        voting_account.options = options;

        // Initialize vote counters
        voting_account.votes = vec![0; voting_account.options.len()];
        voting_account.total_votes = 0;

        Ok(())
    }

    /// Casts a vote for a specific option.
    ///
    /// Creates a VoteAccount (PDA) tied to the voting and the voter wallet.
    /// This guarantees that each user can vote only once.
    ///
    /// Parameters:
    /// - company_id: company identifier (used for PDA derivation)
    /// - voting_id: voting identifier (used for PDA derivation)
    /// - selected_option: index of the selected option
    pub fn vote(
        ctx: Context<Vote>,
        _company_id: u64,
        _voting_id: u64,
        selected_option: u8,
    ) -> Result<()> {
        let voting = &mut ctx.accounts.voting;

        // Ensure selected option exists
        require!(
            (selected_option as usize) < voting.options.len(),
            VotingError::InvalidOption
        );

        // Store user's vote
        let vote_account = &mut ctx.accounts.vote;
        vote_account.voter = ctx.accounts.voter.key();
        vote_account.selected_option = selected_option;

        // Update voting results
        voting.votes[selected_option as usize] += 1;
        voting.total_votes += 1;

        Ok(())
    }
}

/// Accounts context for initializing a voting.
///
/// Creates a VotingAccount PDA using:
/// seeds = ["voting", company_id, voting_id]
#[derive(Accounts)]
#[instruction(company_id: u64, voting_id: u64)]
pub struct InitializeVoting<'info> {
    /// Voting account (PDA).
    /// Created once per company + voting.
    #[account(
        init,
        payer = authority,
        space = VotingAccount::SPACE,
        seeds = [
            b"voting", 
            company_id.to_le_bytes().as_ref(), 
            voting_id.to_le_bytes().as_ref()
        ],
        bump
    )]
    pub voting: Account<'info, VotingAccount>,

    /// Account paying for PDA creation
    #[account(mut)]
    pub authority: Signer<'info>,

    /// System program (required for account initialization)
    pub system_program: Program<'info, System>,
}

/// Accounts context for voting.
///
/// Uses an existing VotingAccount and creates a VoteAccount PDA
/// for the current voter.
#[derive(Accounts)]
#[instruction(company_id: u64, voting_id: u64)]
pub struct Vote<'info> {
    /// Existing voting account
    #[account(
        mut,
        seeds = [
            b"voting", 
            company_id.to_le_bytes().as_ref(), 
            voting_id.to_le_bytes().as_ref()
        ],
        bump
    )]
    pub voting: Account<'info, VotingAccount>,

    /// Vote account (PDA).
    /// Ensures one vote per user.
    #[account(
        init,
        payer = voter,
        space = VoteAccount::SPACE,
        seeds = [b"vote", voting.key().as_ref(), voter.key().as_ref()],
        bump
    )]
    pub vote: Account<'info, VoteAccount>,

    /// User casting the vote
    #[account(mut)]
    pub voter: Signer<'info>,

    /// System program
    pub system_program: Program<'info, System>,
}

/// Main voting account.
/// Stores voting configuration and results.
#[account]
pub struct VotingAccount {
    pub company_id: u64,
    pub voting_id: u64,
    pub question: String,
    pub options: Vec<String>,
    pub votes: Vec<u64>,
    pub total_votes: u64,
}

impl VotingAccount {
    /// Reserved account size (in bytes).
    ///
    /// calculated for:
    /// - question up to ~256 bytes
    /// - up to 3 options, ~64 bytes each
    pub const SPACE: usize =
        8 + // discriminator
        8 + // company_id
        8 + // voting_id
        4 + 256 + // question
        4 + (3 * (4 + 64)) + // options
        4 + (3 * 8) + // votes
        8; // total_votes
}

/// User vote account.
/// Prevents multiple votes from the same wallet.
#[account]
pub struct VoteAccount {
    pub voter: Pubkey,
    pub selected_option: u8,
}

impl VoteAccount {
    /// Reserved account size
    pub const SPACE: usize =
        8 + // discriminator
        32 + // voter pubkey
        1; // selected_option
}

/// Custom program errors
#[error_code]
pub enum VotingError {
    /// Invalid number of answer options
    #[msg("Invalid number of options. Must be 2 or 3.")]
    InvalidOptionsCount,

    /// Selected option index is out of range
    #[msg("Selected option does not exist.")]
    InvalidOption,
}
