using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace SolanaVoting.Client
{
    internal class Program
    {
        // =====================================================
        // ================== CONFIG ===========================
        // =====================================================

        /// <summary>
        /// Solana RPC endpoint (Testnet)
        /// </summary>
        private const string RpcUrl = "https://api.testnet.solana.com";

        /// <summary>
        /// Path to Solana wallet keypair (id.json)
        /// </summary>
        private const string WalletPath = @"C:\id.json";

        /// <summary>
        /// Voting configuration (used as PDA seeds and instruction params)
        /// </summary>
        private const ulong CompanyId = 1;
        private const ulong VotingId = 1;
        private const byte SelectedOption = 0; // index of option (0-based)

        private static readonly string Question = "Do you like Solana?";
        private static readonly string[] Options = ["Yes", "No"];

        /// <summary>
        /// Entry point of console application.
        /// </summary>
        static async Task Main(string[] args)
        {
            // ---------------- LOAD WALLET ----------------

            if (!File.Exists(WalletPath))
            {
                Console.WriteLine($"Wallet file not found: {WalletPath}");
                return;
            }

            // Solana CLI wallet file contains 64 integers (ed25519 keypair)
            var json = await File.ReadAllTextAsync(WalletPath);
            var keyInts = JsonSerializer.Deserialize<int[]>(json);

            if (keyInts is null || keyInts.Length != 64)
            {
                Console.WriteLine("id.json must contain exactly 64 numbers");
                return;
            }

            var secretKey = keyInts.Select(i => (byte)i).ToArray();

            // Solnet expects Base58 encoded secret key
            var account = Account.FromSecretKey(
                Solnet.Wallet.Utilities.Encoders.Base58.EncodeData(secretKey));

            Console.WriteLine("Wallet loaded");
            Console.WriteLine($"Public Key: {account.PublicKey}");

            // ---------------- RPC CLIENT ----------------

            var rpcClient = ClientFactory.GetClient(RpcUrl);

            var balanceResult = await rpcClient.GetBalanceAsync(account.PublicKey);

            if (!balanceResult.WasSuccessful)
            {
                Console.WriteLine($"Failed to get balance: {balanceResult.Reason}");
                return;
            }

            var sol = balanceResult.Result.Value / 1_000_000_000m;
            Console.WriteLine($"Balance: {sol} SOL");

            // ---------------- PROGRAM & PDA ----------------

            var programId = new PublicKey("31RBt6nsdi6tEbKVffYi8CbT8HeLYQgdGyZo8J8uyP6k");

            var votingPda = GetVotingPda(programId, CompanyId, VotingId, out _);
            var votePda = GetVotePda(programId, votingPda, account.PublicKey, out _);

            Console.WriteLine($"Voting PDA: {votingPda}");
            Console.WriteLine($"Vote PDA: {votePda}");

            // ---------------- INIT VOTING ----------------

            var votingInfo = await rpcClient.GetAccountInfoAsync(votingPda);

            if (votingInfo.Result?.Value == null)
            {
                Console.WriteLine("Initializing voting...");

                var signature = await InitializeVoting(
                    rpcClient,
                    account,
                    programId,
                    votingPda,
                    CompanyId,
                    VotingId,
                    Question,
                    Options);

                await WaitForConfirmation(rpcClient, signature);
            }
            else
            {
                Console.WriteLine("Voting exists");
            }

            // ---------------- SEND VOTE ----------------

            var voteInfo = await rpcClient.GetAccountInfoAsync(votePda);

            if (voteInfo.Result?.Value == null)
            {
                Console.WriteLine("Sending vote...");

                var signature = await SendVote(
                    rpcClient,
                    account,
                    programId,
                    votingPda,
                    votePda,
                    CompanyId,
                    VotingId,
                    SelectedOption);

                await WaitForConfirmation(rpcClient, signature);
            }
            else
            {
                Console.WriteLine("User already voted");
            }

            // ---------------- READ RESULT ----------------

            await ReadAndPrintVoting(rpcClient, votingPda);
        }

        // =====================================================
        // ================= SOLANA TX =========================
        // =====================================================

        /// <summary>
        /// Sends initialize_voting instruction to Solana program.
        /// Creates voting PDA account and stores question + options.
        /// </summary>
        private static async Task<string> InitializeVoting(
            IRpcClient rpc,
            Account account,
            PublicKey programId,
            PublicKey votingPda,
            ulong companyId,
            ulong votingId,
            string question,
            string[] options)
        {
            var data = new List<byte>();

            data.AddRange(AnchorDiscriminator("initialize_voting"));
            data.AddRange(BitConverter.GetBytes(companyId));
            data.AddRange(BitConverter.GetBytes(votingId));
            AddString(data, question);

            data.AddRange(BitConverter.GetBytes(options.Length));
            foreach (var option in options)
                AddString(data, option);

            var instruction = new TransactionInstruction
            {
                ProgramId = programId,
                Data = [.. data],
                Keys =
                [
                    AccountMeta.Writable(votingPda, false),
                    AccountMeta.Writable(account.PublicKey, true),
                    AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false)
                ]
            };

            var blockHash = await rpc.GetLatestBlockHashAsync();

            var tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(account)
                .AddInstruction(instruction)
                .Build(account);

            var result = await rpc.SendTransactionAsync(tx);

            if (!result.WasSuccessful || string.IsNullOrWhiteSpace(result.Result))
                throw new Exception($"Init failed: {result.Reason}");

            Console.WriteLine($"Init TX: {result.Result}");
            return result.Result;
        }

        /// <summary>
        /// Sends vote instruction. Creates vote PDA and increments vote counter.
        /// </summary>
        private static async Task<string> SendVote(
            IRpcClient rpc,
            Account account,
            PublicKey programId,
            PublicKey votingPda,
            PublicKey votePda,
            ulong companyId,
            ulong votingId,
            byte selectedOption)
        {
            var data = new List<byte>();

            data.AddRange(AnchorDiscriminator("vote"));
            data.AddRange(BitConverter.GetBytes(companyId));
            data.AddRange(BitConverter.GetBytes(votingId));
            data.Add(selectedOption);

            var instruction = new TransactionInstruction
            {
                ProgramId = programId,
                Data = [.. data],
                Keys =
                [
                    AccountMeta.Writable(votingPda, false),
                    AccountMeta.Writable(votePda, false),
                    AccountMeta.Writable(account.PublicKey, true),
                    AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false)
                ]
            };

            var blockHash = await rpc.GetLatestBlockHashAsync();

            var tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(account)
                .AddInstruction(instruction)
                .Build(account);

            var result = await rpc.SendTransactionAsync(tx);

            if (!result.WasSuccessful || string.IsNullOrWhiteSpace(result.Result))
                throw new Exception($"Vote failed: {result.Reason}");

            Console.WriteLine($"Vote TX: {result.Result}");
            return result.Result;
        }

        /// <summary>
        /// Waits until transaction is confirmed or finalized.
        /// </summary>
        private static async Task WaitForConfirmation(IRpcClient rpc, string signature)
        {
            Console.WriteLine("Waiting confirmation...");

            for (int i = 0; i < 15; i++)
            {
                await Task.Delay(1000);

                var status = await rpc.GetSignatureStatusesAsync([signature]);

                if (!status.WasSuccessful ||
                    status.Result?.Value == null ||
                    status.Result.Value.Count == 0)
                    continue;

                var conf = status.Result.Value[0];

                if (conf?.ConfirmationStatus == "confirmed" ||
                    conf?.ConfirmationStatus == "finalized")
                {
                    Console.WriteLine("Transaction confirmed");
                    return;
                }
            }

            Console.WriteLine("Confirmation timeout");
        }

        // =====================================================
        // ================= READ DATA =========================
        // =====================================================

        /// <summary>
        /// Reads voting account data from blockchain and prints results.
        /// </summary>
        private static async Task ReadAndPrintVoting(IRpcClient rpc, PublicKey votingPda)
        {
            var acc = await rpc.GetAccountInfoAsync(votingPda);

            if (acc.Result?.Value == null)
                return;

            var bytes = Convert.FromBase64String(acc.Result.Value.Data[0]);
            var voting = DecodeVotingAccount(bytes);

            Console.WriteLine($"Question: {voting.Question}");

            for (int i = 0; i < voting.Options.Length; i++)
                Console.WriteLine($"{voting.Options[i]} = {voting.Votes[i]}");

            Console.WriteLine($"Total: {voting.TotalVotes}");
        }

        // =====================================================
        // ================= BINARY HELPERS ====================
        // =====================================================

        /// <summary>
        /// Anchor instruction discriminator (first 8 bytes of SHA256("global:name"))
        /// </summary>
        private static byte[] AnchorDiscriminator(string name)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes("global:" + name));

            return hash[..8];
        }

        /// <summary>
        /// Writes Anchor-compatible string (u32 length + UTF8 bytes)
        /// </summary>
        private static void AddString(List<byte> buffer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            buffer.AddRange(BitConverter.GetBytes(bytes.Length));
            buffer.AddRange(bytes);
        }

        /// <summary>
        /// Decodes voting account data according to Rust struct layout.
        /// </summary>
        private static VotingData DecodeVotingAccount(byte[] data)
        {
            int offset = 8; // skip discriminator

            _ = ReadU64(data, ref offset); // companyId
            _ = ReadU64(data, ref offset); // votingId

            string question = ReadString(data, ref offset);

            int optionCount = ReadU32(data, ref offset);
            var options = new string[optionCount];

            for (int i = 0; i < optionCount; i++)
                options[i] = ReadString(data, ref offset);

            int voteCount = ReadU32(data, ref offset);
            var votes = new ulong[voteCount];

            for (int i = 0; i < voteCount; i++)
                votes[i] = ReadU64(data, ref offset);

            ulong totalVotes = ReadU64(data, ref offset);

            return new VotingData
            {
                Question = question,
                Options = options,
                Votes = votes,
                TotalVotes = totalVotes
            };
        }

        /// <summary>
        /// Reads an unsigned 64-bit integer (little-endian) from the byte array and advances the offset.
        /// </summary>
        private static ulong ReadU64(byte[] data, ref int offset)
        {
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(
                data.AsSpan(offset, 8));

            offset += 8;
            return value;
        }

        /// <summary>
        /// Reads a 32-bit integer (little-endian) from the byte array and advances the offset.
        /// </summary>
        private static int ReadU32(byte[] data, ref int offset)
        {
            int value = BinaryPrimitives.ReadInt32LittleEndian(
                data.AsSpan(offset, 4));

            offset += 4;
            return value;
        }

        /// <summary>
        /// Reads a UTF-8 string from the byte array and advances the offset.
        /// </summary>
        private static string ReadString(byte[] data, ref int offset)
        {
            int length = ReadU32(data, ref offset);
            string value = Encoding.UTF8.GetString(data, offset, length);
            offset += length;
            return value;
        }

        // =====================================================
        // ================= PDA HELPERS =======================
        // =====================================================

        /// <summary>
        /// Derives Voting PDA using seeds: "voting", companyId, votingId
        /// </summary>
        private static PublicKey GetVotingPda(
            PublicKey programId,
            ulong companyId,
            ulong votingId,
            out byte bump)
        {
            var seed1 = Encoding.UTF8.GetBytes("voting");

            var seed2 = new byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(seed2, companyId);

            var seed3 = new byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(seed3, votingId);

            PublicKey.TryFindProgramAddress(
                [seed1, seed2, seed3],
                programId,
                out var pda,
                out bump);

            return pda;
        }

        /// <summary>
        /// Derives Vote PDA using seeds: "vote", votingPda, voterPubkey
        /// </summary>
        private static PublicKey GetVotePda(
            PublicKey programId,
            PublicKey voting,
            PublicKey voter,
            out byte bump)
        {
            var seed1 = Encoding.UTF8.GetBytes("vote");

            PublicKey.TryFindProgramAddress(
                [seed1, voting.KeyBytes, voter.KeyBytes],
                programId,
                out var pda,
                out bump);

            return pda;
        }
    }

    /// <summary>
    /// DTO representing decoded voting account state.
    /// </summary>
    internal class VotingData
    {
        public string Question { get; set; } = "";
        public string[] Options { get; set; } = Array.Empty<string>();
        public ulong[] Votes { get; set; } = Array.Empty<ulong>();
        public ulong TotalVotes { get; set; }
    }
}
