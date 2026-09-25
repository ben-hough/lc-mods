# SharedWaypoints

Lethal Company BepInEx QoL mod — drop a waypoint that appears on every modded player's fixed top-right HUD panel **and** on the ship radar / map video feeds (including CrewMonitors dedicated map feeds that copy the main map camera culling mask).

**Requires:** BepInExPack (v81 game)

## Use

| Key | Action |
|-----|--------|
| **F8** (`DropKey`) | Place / replace your waypoint for the current zone (indoor or outdoor) |
| **F7** (`ClearKey`) | Clear your waypoint for the current zone for everyone |

Each player can keep **1 indoor + 1 outdoor** pin. Synced over Unity Netcode to all clients with the mod.

## HUD vs map

- **HUD (top-right):** lists pins in your current zone (indoor/outdoor filter), with distance and relative bearing.
- **Ship radar / map feeds:** world-space markers under the same UI layer as vanilla door codes, so the main `mapScreen.mapCamera` and any camera that shares its culling mask (e.g. CrewMonitors map feeds) can see them. Indoor and outdoor pins stay active; the map camera location / clip planes determine what is in view.

## Config (`BepInEx/config/com.benhough.lethal.SharedWaypoints.cfg`)

- `Enabled` — master toggle
- `DropKey` / `ClearKey` — defaults F8 / F7
- `MaxDistance` — hide markers beyond this (default 500 m) on HUD and map
- `ShowOwnWaypoint` — show your own pin on HUD and map (default true)
- `HudScale` — HUD panel scale (default 1)

## Notes

- **Host requirement:** the lobby host should run this mod so late-join sync and shared pins work for everyone with the mod.
- Peers without the mod simply won't send or see pins.
- Late joiners receive a resync of existing waypoints from the host.
- All waypoints clear when leaving the moon, entering ship phase, or disconnecting.
- CrewMonitors is **not** required; map pins work on the vanilla ship radar alone.
