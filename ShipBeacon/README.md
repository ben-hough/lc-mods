# ShipBeacon

Outdoor HUD beacon pointing toward the company ship.

**Thunderstore:** [MrGlim-ShipBeacon](https://thunderstore.io/c/lethal-company/p/MrGlim/ShipBeacon/)  
**Game:** Lethal Company v81 (and compatible)

## Install

1. Install BepInEx Pack for Lethal Company.
2. Drop `ShipBeacon.dll` into `BepInEx/plugins/` (or install via r2modman / Gale).

## Features

- Top-of-screen orange HUD (color matched to the in-game clock)
- Direction cues:
  - **Ahead:** `^  SHIP  42m  ^`
  - **Left / right:** `<<<  SHIP  42m` or `SHIP  42m  >>>` (chevrons only on the pointing side)
  - **Behind:** `v  SHIP  42m  v`
- Text sits at the bottom of the screen frame (bottom-anchored)
- Hidden in orbit, inside the factory, and while inside the ship

## Config (`BepInEx/config/com.benhough.lethal.ShipBeacon.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `ShowDistance` | true | Append meters |
| `HudScale` | 1.0 | Multiplier on font size |
| `VerticalOffset` | 0.10 | Bottom of beacon text (0 = bottom, 1 = top) |
| `MatchClockStyle` | true | Prefer clock TMP font when available |

## Build

```bash
dotnet build -c Release
```

## License

MIT — see `LICENSE`.
