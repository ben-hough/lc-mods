# KeepItemsOnTeleport

Keep inventory items when beamed by the ship teleporter (normal and inverse). Host recommended.

**Thunderstore:** [MrGlim-KeepItemsOnTeleport](https://thunderstore.io/c/lethal-company/p/MrGlim/KeepItemsOnTeleport/)  
**Source:** [lc-keep-items-on-teleport](https://github.com/ben-hough/lc-keep-items-on-teleport)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Prevents inventory drop on ship teleporter beam-in
- Works for normal and inverse teleporters (configurable)
- Lightweight Harmony patch — no new items or UI

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-KeepItemsOnTeleport** via Thunderstore / r2modman / Gale, or drop `KeepItemsOnTeleport.dll` into `BepInEx/plugins/`.

Host should run this so item retention applies for teleported players.

## Config (`BepInEx/config/com.benhough.lethal.KeepItemsOnTeleport.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `KeepOnNormalTeleporter` | true | Keep items on normal TP |
| `KeepOnInverseTeleporter` | true | Keep items on inverse TP |
| `VerboseLogging` | false | Log when a drop is skipped |

## Changelog

### 1.0.1
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
