# KeepItemsOnTeleport

Keep your held/inventory items when the ship teleporter beams you.

**Thunderstore:** [MrGlim-KeepItemsOnTeleport](https://thunderstore.io/c/lethal-company/p/MrGlim/KeepItemsOnTeleport/)  
**Game:** Lethal Company v81 (and compatible)

## What it does

- **Normal teleporter** (facility → ship): items stay in your slots
- **Inverse teleporter** (ship → facility): items stay in your slots
- Death / disconnect still drops items as vanilla

## Install

1. Install BepInEx Pack for Lethal Company.
2. Drop `KeepItemsOnTeleport.dll` into `BepInEx/plugins/` (or install via Thunderstore Mod Manager / Gale).

**Lobby note:** everyone in the lobby should run this mod (teleport inventory is synced).

## Config (`BepInEx/config/com.benhough.lethal.KeepItemsOnTeleport.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `KeepOnNormalTeleporter` | true | Keep items when beamed to the ship |
| `KeepOnInverseTeleporter` | true | Keep items when beamed into the facility |
| `VerboseLogging` | false | Log skipped drops |

## Build

```bash
dotnet build -c Release
```

## License

MIT — see `LICENSE`.
