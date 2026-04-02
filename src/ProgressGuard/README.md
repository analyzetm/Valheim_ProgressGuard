# ProgressGuard

ProgressGuard is a server/host-enforced Valheim mod for BepInEx that blocks undefeated boss summons until:

- enough players are online
- a `/agree` vote succeeds

The vote unlocks only the next summon attempt. It does not auto-spawn the boss.

## Build Instructions

1. Open `src\ProgressGuard\ProgressGuard.csproj` in Visual Studio.
2. Build the project in `Release` or `Debug`.
3. The project targets `.NET Framework 4.7.2` and references Valheim/BepInEx DLLs from:
   `C:\Program Files (x86)\Steam\steamapps\common\Valheim`

## Output DLL Location

The project is configured with:

- `OutputPath = ..\..\BepInEx\plugins\`
- `AppendTargetFrameworkToOutputPath = false`

With the provided folder layout, building the project writes `ProgressGuard.dll` directly to:

- `BepInEx\plugins\`

relative to this Valheim copy:

- `C:\Program Files (x86)\Steam\steamapps\common\Valheim - Kopie`

## Installing The Mod

If you build this project inside the provided Valheim copy, no extra copy step is needed because the DLL outputs straight into:

- `BepInEx\plugins\`

If you build elsewhere, place `ProgressGuard.dll` into:

- `BepInEx\plugins\`

on the server or host installation that will enforce the rules.

## Configuration

The plugin creates BepInEx config entries for:

- `MinPlayersRequired` default `2`
- `MinAgreesRequired` default `2`
- `VoteTimeoutSeconds` default `60`
- `DebugLogging` default `true`

## How To Test

1. Start Valheim with BepInEx on a host or dedicated server.
2. Join with at least two players.
3. Go to an undefeated boss altar.
4. Attempt to summon the boss.
5. Confirm the summon is blocked and the server broadcasts:
   `Boss summon locked. Type agree or /agree to approve.`
6. Have players type `agree` or `/agree` in chat.
7. Confirm the server broadcasts approval after enough unique agrees:
   `Boss summon approved. Try again.`
8. Attempt the summon again and confirm the next attempt succeeds.
9. Try a summon after the boss is already defeated and confirm it is allowed immediately.
10. Start a vote and wait past the timeout to confirm:
    `Vote expired.`

## Logs

BepInEx logs are written to:

- `BepInEx\LogOutput.log`

for the active Valheim installation.

For this workspace, that file is:

- `C:\Program Files (x86)\Steam\steamapps\common\Valheim - Kopie\BepInEx\LogOutput.log`

## Version Notes

This implementation patches these altar methods for the current inspected Valheim build:

- `OfferingBowl.RPC_SpawnBoss`
- `OfferingBowl.InitiateSpawnBoss`

It also traces:

- `OfferingBowl.Interact`
- `OfferingBowl.UseItem`

Valheim altar flow can change between versions. If Iron Gate moves the summon path, use the included debug logging to confirm which `OfferingBowl` method actually fires before a boss spawn and retarget the Harmony patches as needed.

## Behavior Summary

- Defeated boss: summon allowed immediately.
- Undefeated boss with too few players online: summon blocked.
- Undefeated boss with enough players online but no approved vote: summon blocked and vote starts.
- Successful vote: only the next summon attempt is unlocked.
- Approval is consumed on the next allowed summon attempt.
