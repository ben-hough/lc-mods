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
  - **Left / right:** `<<<  SHIP  42m` or `SHIP  42m  >>>` (chevrons only on the pointing side; count 1–4 by turn angle)
  - **Behind:** `v  SHIP  42m  v`
- Optional distance in meters
- Hidden in orbit, inside the factory, and while inside the ship

## Config (`BepInEx/config/com.benhough.lethal.ShipBeacon.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `ShowDistance` | true | Append meters |
| `HudScale` | 1.0 | Multiplier on clock-matched font size |
| `VerticalOffset` | 0.9 | Screen Y anchor (0 = bottom, 1 = top). Above-clock placement works well around `0.9` |
| `MatchClockStyle` | true | Prefer clock TMP font when available |

## Troubleshooting

- Wrong color / white text: use **1.0.10+** (older builds could steal clock face/material tint).
- Not showing outdoors: check `LogOutput.log` for `ShipBeacon hidden:` reasons (`inShipPhase`, `inside factory`, etc.).

## Build

```bash
dotnet build -c Release
```

## License

MIT — see `LICENSE`.
