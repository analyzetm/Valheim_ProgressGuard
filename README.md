# ProgressGuard

ProgressGuard is a server-side Valheim BepInEx mod that prevents solo boss rushing by locking undefeated boss summons behind a multiplayer approval flow.

## What It Does

- blocks boss summons if the boss has not already been defeated
- requires a minimum number of players online
- requires unique `agree` or `/agree` votes from players
- unlocks only the next summon attempt after approval
- never auto-spawns the boss
- enforces the rules on the host or server side

## Project Layout

- `src/ProgressGuard/`
  Visual Studio C# project for the mod source

## Build

1. Open `src/ProgressGuard/ProgressGuard.csproj` in Visual Studio.
2. Build against your local Valheim and BepInEx install.
3. The current project targets `.NET Framework 4.7.2`.

## Configuration

The mod exposes these BepInEx config entries:

- `MinPlayersRequired`
- `MinAgreesRequired`
- `VoteTimeoutSeconds`
- `DebugLogging`

## How The Vote Works

1. A player tries to summon an undefeated boss.
2. The summon is blocked.
3. ProgressGuard starts a vote.
4. Players type `agree` or `/agree` in chat.
5. Once enough unique approvals are collected, the next summon attempt is allowed.
6. The approval is consumed by that next attempt and then resets.

## Notes

- The repository contains source code only.
- Compiled DLLs and Thunderstore packaging artifacts are not committed as release binaries.
- Runtime logs can be checked in `BepInEx/LogOutput.log`.
