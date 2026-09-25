# ShipDoorTerminal

Terminal commands door / opendoor / closedoor for the ship hangar doors. Host recommended.

**Thunderstore:** [MrGlim-ShipDoorTerminal](https://thunderstore.io/c/lethal-company/p/MrGlim/ShipDoorTerminal/)  
**Source:** [lc-ship-door-terminal](https://github.com/ben-hough/lc-ship-door-terminal)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Terminal: `door` / `doors` / `opendoor` / `closedoor`
- Toggles or forces ship hangar door state from the terminal
- Lightweight — no new assets

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-ShipDoorTerminal** via Thunderstore / r2modman / Gale, or drop `ShipDoorTerminal.dll` into `BepInEx/plugins/`.

Host should run this so door commands sync for the lobby.

## Config (`BepInEx/config/com.benhough.lethal.ShipDoorTerminal.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Enable door terminal commands |
| `VerboseLogging` | true | Log terminal/door traces |

## Changelog

### 1.0.11
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
