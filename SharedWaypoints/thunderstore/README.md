# SharedWaypoints

Drop shared waypoints for your crew — fixed top-right HUD **and** ship radar / map video feeds (including CrewMonitors map feeds). Indoor/outdoor aware; F8 drop, F7 clear. Host syncs markers.

**Thunderstore:** [MrGlim-SharedWaypoints](https://thunderstore.io/c/lethal-company/p/MrGlim/SharedWaypoints/)  
**Source:** [lc-shared-waypoints](https://github.com/ben-hough/lc-shared-waypoints)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Shared waypoint on HUD and ship radar / map feeds
- Works with CrewMonitors map feeds (no soft dependency — same world markers as door codes)
- Indoor/outdoor aware (1 indoor + 1 outdoor pin per player)
- F8 drop / F7 clear (configurable keys)
- Configurable max distance and HUD scale

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-SharedWaypoints** via Thunderstore / r2modman / Gale, or drop `SharedWaypoints.dll` into `BepInEx/plugins/`.

Host should run this so waypoints sync to the lobby.

## Config (`BepInEx/config/com.benhough.lethal.SharedWaypoints.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `DropKey` | F8 | Drop waypoint (current zone) |
| `ClearKey` | F7 | Clear your current-zone waypoint |
| `MaxDistance` | 500 | Max HUD/map draw distance |
| `ShowOwnWaypoint` | true | Show your own marker on HUD and map |
| `HudScale` | 1.0 | HUD scale multiplier |

## Changelog

### 1.0.4
- Waypoint pins on ship radar / map video feeds (vanilla map camera + CrewMonitors map feeds that copy culling).
- HUD and networking unchanged.

### 1.0.3
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
