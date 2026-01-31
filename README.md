# SolanaVoting

SolanaVoting is a Proof of Concept (PoC) of an on-chain voting system built on the Solana blockchain.  
The project demonstrates a complete voting flow, including smart contract logic and a client application interacting with the blockchain.

The main goal of this project is to explore Solana Program Derived Addresses (PDAs), on-chain data storage, and client–contract interaction using a real-world–like voting scenario.

---

## 🎯 Project Goals

- Design and implement a simple on-chain voting system on Solana
- Learn and apply Anchor framework fundamentals
- Use Program Derived Addresses (PDAs) for deterministic account creation
- Prevent double voting using on-chain constraints
- Implement a client application that interacts with the smart contract
- Understand the full lifecycle of deploying and using a Solana program

---

## 🛠 Technology Stack

#### Smart Contract
- Rust
- Solana
- Anchor framework

#### Client
- C#
- .NET
- Solnet (Solana RPC and wallet SDK)

#### Tooling
- Solana CLI
- Anchor CLI

--- 

## 🚀 How to Run

<details>
<summary>Click to expand installation and run instructions</summary>

#### 1️⃣ Prerequisites

Make sure you have the following installed:

##### Rust & Anchor (Smart Contract)
- Rust: https://www.rust-lang.org/tools/install  
- Solana CLI: https://docs.solana.com/cli/install-solana-cli-tools  
- Anchor: https://project-serum.github.io/anchor/getting-started/installation.html  

##### C# Client
- .NET 8+ SDK: https://dotnet.microsoft.com/download  
- IDE: Visual Studio or VS Code

#### 2️⃣ Setting Up Solana Testnet Wallet

```bash
solana-keygen new --outfile ~/id.json
solana config set --url https://api.testnet.solana.com
solana airdrop 2 <YOUR_PUBLIC_KEY>
```

Save the path to your keypair (for example `~/id.json` or `C:\id.json`) for the client.

#### 3️⃣ Deploy Smart Contract

```bash
cd src/SolanaVoting.SmartContract
anchor build
anchor deploy
```

Take note of the program ID printed during deployment. <br>
Make sure it matches the `programId` in the C# client (`Program.cs`)

#### 4️⃣ Run the C# Client

- Open the client project: `src/SolanaVoting.Client/SolanaVoting.Client.sln`
- Update `Program.cs` if needed: set
  - `RpcUrl` (testnet)
  - `WalletPath` (path to your keypair)
  - `companyId`, `votingId`, `selectedOption`
- Run the project (console app). Example:

```bash
dotnet run --project src/SolanaVoting.Client/SolanaVoting.Client.csproj
```

- The client will:
  - Check wallet balance
  - Initialize voting (if not exists)
  - Cast your vote
  - Print voting results

</details>

---

## 📂 Repository Structure

```text
SolanaVoting/
├── .gitignore
├── LICENSE
├── README.md
└── src/
    ├── SolanaVoting.Client/
    │   ├── Program.cs
    │   ├── SolanaVoting.Client.csproj
    │   └── SolanaVoting.Client.sln
    │
    └── SolanaVoting.SmartContract/
        ├── Anchor.toml
        ├── Cargo.toml
        ├── Cargo.lock
        └── programs/
            └── voting/
                ├── Cargo.toml
                └── src/
                    └── lib.rs
```

---

## ⚠️ Limitations and Simplifications

This project is a Proof of Concept and includes several intentional simplifications:

- No authentication or identity verification beyond wallet signatures
- No voting expiration or time-based restrictions
- Voting options are limited to 2–3 predefined answers
- No on-chain validation for duplicate voting beyond PDA-based constraints
- No frontend UI; interaction is done via a console client
- No upgrade mechanism for the deployed smart contract

These limitations are acceptable for a PoC and help keep the focus on core Solana concepts.

--- 

## 📦 Third-party Dependencies

This project uses the following third-party libraries and tools, all of which are compatible with commercial use:

#### Rust / Solana
- Anchor — Apache 2.0 License
- Solana SDK — Apache 2.0 License

#### C# Client
- Solnet — MIT License

All dependencies are used in accordance with their respective licenses.

---

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/excleake/SolanaVoting/blob/master/LICENSE) file for details.
