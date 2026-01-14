# Solana Voting PoC

Proof of Concept (PoC) of a simple on-chain voting system built on the **Solana blockchain**.

The goal of this project is to demonstrate:
- a basic **Solana smart contract (program)** written in **Rust**
- interaction with the contract from an **off-chain client**
- end-to-end workflow on **Solana Testnet**

This repository is created as part of an internal technical task and learning process.

---

## 📌 Project Scope

The voting system supports:
- a single voting question
- 2–3 predefined answer options
- recording votes on-chain
- reading vote results from the blockchain

### On-chain (Solana Program)
- Written in **Rust**
- Uses the **Anchor framework**
- Stores voting data inside Solana accounts
- Deployed to **Solana Testnet**

### Off-chain (Client)
- Client application for submitting votes
- Planned implementations:
  - **JavaScript** (for testing and validation)
  - **C#** (main client, using Solana RPC)

---

## 🛠 Technology Stack

- **Rust** — smart contract development
- **Solana CLI** — wallet management, deployment
- **Anchor** — Solana program framework
- **Node.js / Yarn** — testing and scripting
- **C# (.NET)** — off-chain client (planned)
- **WSL2 + Ubuntu 22.04** — development environment on Windows

---

## 🧱 Development Environment Setup (Completed)

The following setup steps have been completed:

- ✅ Windows 11 with **WSL2**
- ✅ **Ubuntu 22.04 LTS** installed via Microsoft Store
- ✅ **Rust toolchain** installed via rustup
- ✅ **Solana CLI** installed (Agave release)
- ✅ **Anchor CLI** installed
- ✅ **Node.js** and **Yarn** installed
- ✅ Local Solana tooling verified (`solana`, `anchor`, `rustc`, `node`)
- ⏳ Solana Testnet wallet setup (next step)

---

## 📂 Planned Repository Structure

```text
/
├── programs/
│   └── voting/          # Solana smart contract (Rust, Anchor)
├── client-js/           # JS client for testing (Anchor tests / scripts)
├── client-csharp/       # C# client for interacting with the contract
├── docs/                # Notes, architecture, decisions
└── README.md

## 📜 Licenses and Third-Party Dependencies

This project uses the following third-party tools and libraries, all of which are compatible with commercial use:

- **Solana CLI** — Apache 2.0
- **Anchor Framework** — Apache 2.0
- **Rust & Cargo** — MIT / Apache 2.0
- **Node.js** — MIT
- **Yarn** — BSD
- **Solana Web3.js (planned)** — Apache 2.0

All dependencies used in this project allow commercial usage.
