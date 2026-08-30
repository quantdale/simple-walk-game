# Fresh-machine onboarding

This is the canonical bootstrap entry point for a new workstation or a fresh coding-agent environment. Complete this document before implementation work. The objective is a reproducible machine that can build, test, inspect, and operate this repository without rediscovering tooling mid-campaign.

## 1. Preflight rule

1. Clone the repository and enter its root.
2. Confirm the intended repository/branch and fetch current `origin/main`.
3. Read the repository control-plane documents before changing code: `AGENTS.md`, `README.md`, `docs/AGENT_EXECUTION_GUIDE.md`, `docs/ROADMAP.md`, `.agent/`.
4. Install/verify the machine prerequisites below.
5. Enable the committed agent integrations and repository-local skills.
6. Restore dependencies from lockfiles/pins; do not casually upgrade them during bootstrap.
7. Run the baseline validation commands.
8. Only then begin a development campaign. If a prerequisite cannot be satisfied, record it as an environment blocker rather than weakening a gate.

Credentials, API keys, signing material, account logins, licensed assets, and other secrets are machine/user responsibilities. Never commit them.

## 2. Supported host and prerequisites

**Primary host:** Cross-platform .NET for the deterministic headless core. Unity 6 LTS is required only when executing the presentation/runtime campaign.

**Required machine tools**
- Git
- .NET SDK capable of building `SimpleWalkGame.sln`
- PowerShell and/or Bash for repository guard scripts

**Task-dependent / optional tools**
- Unity 6 LTS editor for the next presentation/runtime campaign
- Android Studio/SDK/JDK for Android device integration
- macOS/Xcode for iOS/HealthKit integration


## 3. Agent setup

- Load repository instructions before acting. Prefer committed repository state over chat history.
- Repository-local skills: `goal`.
- Discover and use committed agent adapter/config directories in-place; do not duplicate them globally unless the harness cannot load repository-local configuration.
- Relevant committed agent surfaces: `.agent/`, `.agents/`, `.claude/`, `.githooks/`, `.kimi-code/`, `.opencode/`, `.repo-identity.json`.
- MCP policy: No root `.mcp.json` is committed. Do not replace the repository's deterministic CLI/simulation and guard rails with generic automation.
- Keep diagnostic/documentation MCPs narrow. An MCP does not grant architecture, publishing, production, or gate-bypass authority.
- Authenticate GitHub and coding-agent CLIs separately on the machine. Never store tokens in tracked files.

## 4. Bootstrap

```bash
dotnet --info
dotnet restore SimpleWalkGame.sln
./scripts/assert-repo-identity.sh
```

Before any write session, obey the repository's single-writer lease/worktree rules. Missing Unity must remain an explicit environment blocker for Unity-specific milestones.


## 5. Editor/LSP baseline

Use Roslyn/C# language support against the solution. For Unity work, use Unity-aware C# project generation but keep the domain layer engine-free.

The editor is optional; reliable language diagnostics are not.

## 6. Baseline verification

```bash
dotnet build SimpleWalkGame.sln
dotnet test SimpleWalkGame.sln
./tests/guards/run-guard-tests.sh
```

A fresh machine is **development-ready** when all applicable non-external gates pass. Hardware/device/signing/account gates may remain explicitly blocked when repository state already classifies them that way.

## 7. Fresh-agent instruction

> Read `ONBOARDING.md` first. Set up every applicable prerequisite, repository-local skill, MCP/plugin, dependency, browser/device/runtime tool, and validation gate described there. Then read the repository's durable agent state and only start implementation after preflight is green or a genuine environment blocker is recorded. Do not replace pinned tooling, skip gates, or invent work to compensate for a missing machine capability.
