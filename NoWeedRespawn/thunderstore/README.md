# NoWeedRespawn

Cleared weeds, mold, and cadaver plants stay gone for the rest of the moon. Host should run it.

**Thunderstore:** [MrGlim-NoWeedRespawn](https://thunderstore.io/c/lethal-company/p/MrGlim/NoWeedRespawn/)  
**Source:** [lc-mods/NoWeedRespawn](https://github.com/ben-hough/lc-mods/tree/main/NoWeedRespawn)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Blocks indoor and outdoor weed/mold/cadaver plant respawn
- Cleared foliage stays cleared for the rest of the moon
- Independent toggles for indoor vs outdoor respawn block

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-NoWeedRespawn** via Thunderstore / r2modman / Gale, or drop `NoWeedRespawn.dll` into `BepInEx/plugins/`.

Host should run this for networked growth/respawn RPCs.

## Config (`BepInEx/config/com.benhough.lethal.NoWeedRespawn.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `BlockIndoorRespawn` | true | Block indoor plant respawn |
| `BlockOutdoorRespawn` | true | Block outdoor plant respawn |
| `VerboseLogging` | false | Extra logs |

## Changelog

### 1.0.1
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
