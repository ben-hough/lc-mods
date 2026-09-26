# NoMineshaft

Removes Mineshaft interiors from dungeon rotation. Host must run this for moon generation.

**Thunderstore:** [MrGlim-NoMineshaft](https://thunderstore.io/c/lethal-company/p/MrGlim/NoMineshaft/)  
**Source:** [lc-no-mineshaft](https://github.com/ben-hough/lc-no-mineshaft)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Scrubs Mineshaft (dungeon id 4 / Level3Flow) from moon dungeon lists
- Intercepts floor generation so Mineshaft does not slip back in
- Clients benefit automatically when the lobby host has it

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-NoMineshaft** via Thunderstore / r2modman / Gale, or drop `NoMineshaft.dll` into `BepInEx/plugins/`.

Host must run this — dungeon selection is decided on the host.

## Config (`BepInEx/config/com.benhough.lethal.NoMineshaft.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Remove Mineshaft from rotation |
| `VerboseLogging` | true | Extra scrub/watcher logs |

## Changelog

### 1.0.7
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
