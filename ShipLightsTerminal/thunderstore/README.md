# ShipLightsTerminal

Terminal commands lights / lightson / lightsoff to control ship lights. Host recommended.

**Thunderstore:** [MrGlim-ShipLightsTerminal](https://thunderstore.io/c/lethal-company/p/MrGlim/ShipLightsTerminal/)  
**Source:** [lc-mods/ShipLightsTerminal](https://github.com/ben-hough/lc-mods/tree/main/ShipLightsTerminal)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Terminal: `lights` / `lightson` / `lightsoff` / `togglelights`
- Control ship interior lights without leaving the terminal

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-ShipLightsTerminal** via Thunderstore / r2modman / Gale, or drop `ShipLightsTerminal.dll` into `BepInEx/plugins/`.

Host should run this so light toggles sync for the lobby.

## Config (`BepInEx/config/com.benhough.lethal.ShipLightsTerminal.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Enable lights terminal commands |
| `VerboseLogging` | false | Log terminal/lights traces |

## Changelog

### 1.0.2
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
