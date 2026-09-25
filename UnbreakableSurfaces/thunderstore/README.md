# UnbreakableSurfaces

Bridges and unstable platforms never collapse. Host should run it for synced surfaces.

**Thunderstore:** [MrGlim-UnbreakableSurfaces](https://thunderstore.io/c/lethal-company/p/MrGlim/UnbreakableSurfaces/)  
**Source:** [lc-unbreakable-surfaces](https://github.com/ben-hough/lc-unbreakable-surfaces)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Vow-style bridges never collapse
- Unstable platforms / type-2 bridges stay solid
- Independent toggles for bridges vs breakable surfaces

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-UnbreakableSurfaces** via Thunderstore / r2modman / Gale, or drop `UnbreakableSurfaces.dll` into `BepInEx/plugins/`.

Host should run this so surface state matches for all clients.

## Config (`BepInEx/config/com.benhough.lethal.UnbreakableSurfaces.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `Bridges` | true | Vow-style bridges never collapse |
| `BreakableSurfaces` | true | Unstable platforms never fall |

## Changelog

### 1.0.2
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
