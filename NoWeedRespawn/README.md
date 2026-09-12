# NoWeedRespawn

When weeds / mold / cadaver plants are completely cleared from an area, they stay gone for the rest of that moon (day).

## Host note

**The host should run this mod.** Indoor growth uses networked RPCs (`SyncRemoveEradicationFromTileRpc`, `SyncSpawnPlantRpc`); outdoor mold generation is server-driven. Clients alone cannot keep eradication / destroyed-mold state authoritative.

## Config (`BepInEx/config/com.benhough.lethal.NoWeedRespawn.cfg`)

| Key | Default | Meaning |
|-----|---------|---------|
| Enabled | true | Master toggle |
| BlockIndoorRespawn | true | Keep indoor cadaver tiles eradicated after a full clear |
| BlockOutdoorRespawn | true | Stop outdoor vain shroud / mold regen after clears this moon |
| VerboseLogging | false | Log patch hits |

## How it works

### Indoor (cadaver growth)

- Prefix-skips `CadaverGrowthAI.SyncRemoveEradicationFromTileRpc` so vanilla cannot lift `TileWithGrowth.eradicated`.
- After `DestroyPlantAtPosition` / `RemoveWeedFromTile`, if a tile has no plants left, forces `eradicated = true` (server only).
- Prefix-skips `SyncSpawnPlantRpc` when the target tile is eradicated.

### Outdoor (mold / vain shrouds)

- Tracks destroys via `AddToDestroyedMoldList` / `DestroyMoldAtIndex` / `DestroyMoldAtPosition`.
- When no living weeds remain after a destroy this moon, sets an `OutdoorCleared` session flag and Prefix-skips further `MoldSpreadManager.GenerateMold` calls.
- Resets session flags on `ResetMoldData`, `StartOfRound.ResetMoldStates`, and `StartGame`.

## Requirements

- Lethal Company v81
- BepInEx 5
