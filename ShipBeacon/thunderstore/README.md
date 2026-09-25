# ShipBeacon

Outdoor HUD ship direction and distance in orange clock-style text. Pure client-side QoL.

**Thunderstore:** [MrGlim-ShipBeacon](https://thunderstore.io/c/lethal-company/p/MrGlim/ShipBeacon/)  
**Source:** [lc-ship-beacon](https://github.com/ben-hough/lc-ship-beacon)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Client-side HUD — install on each PC that should see the beacon.

## Features

- Bottom-anchored orange HUD pointing toward the company ship
- Direction cues: ahead / left / right / behind with distance
- Hidden in orbit, inside the factory, and while inside the ship
- Optional clock-matched TMP font styling

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-ShipBeacon** via Thunderstore / r2modman / Gale, or drop `ShipBeacon.dll` into `BepInEx/plugins/`.

Client-side only — each player who wants the HUD installs it.

## Config (`BepInEx/config/com.benhough.lethal.ShipBeacon.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Show outdoor ship beacon HUD |
| `ShowDistance` | true | Show distance in meters |
| `HudScale` | 1.0 | Font scale |
| `VerticalOffset` | 0.06 | Vertical position (0=bottom, 1=top) |
| `MatchClockStyle` | true | Prefer clock TMP font |

## Changelog

### 1.0.14
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
