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

## AI disclosure

This mod was made with the help of generative AI. The code and this README were produced with an AI coding agent, directed by the author (MrGlim / Ben Hough). The Thunderstore package is listed in the **AI Generated** category.

## License

MIT — see `LICENSE`.

## Changelog

### 1.0.2
- Fixed multiplayer held-item desync after a ship teleport. Teammates could keep seeing an item in your hand after you had already dropped it, been snared, or died. Keeping items through the beam is unchanged.

### 1.0.1
- Packaging refresh.

### 1.0.0
- Initial release.

