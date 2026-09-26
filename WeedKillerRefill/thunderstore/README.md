# WeedKillerRefill

Refill an empty weed killer can with R (configurable). Syncs tank charge across the lobby.

**Thunderstore:** [MrGlim-WeedKillerRefill](https://thunderstore.io/c/lethal-company/p/MrGlim/WeedKillerRefill/)  
**Source:** [lc-weed-killer-refill](https://github.com/ben-hough/lc-weed-killer-refill)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Press R (configurable) while holding weed killer to refill
- Optional only-when-empty gate and empty threshold
- Shows a control tip when refill is available
- Syncs tank charge for multiplayer

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-WeedKillerRefill** via Thunderstore / r2modman / Gale, or drop `WeedKillerRefill.dll` into `BepInEx/plugins/`.

Host should run this so refill syncs tank charge for everyone.

## Config (`BepInEx/config/com.benhough.lethal.WeedKillerRefill.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Allow key refill |
| `RefillKey` | R | Key to refill |
| `EmptyThreshold` | 0.05 | Charge treated as empty |
| `OnlyWhenEmpty` | true | Only refill near empty |
| `ShowControlTip` | true | Show on-screen tip |
| `AllowKeyRefill` | true | Enable keybind refill |
| `VerboseLogging` | true | Extra logs |

## Changelog

### 1.0.10
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
