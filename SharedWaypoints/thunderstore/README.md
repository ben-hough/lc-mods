# SharedWaypoints

Lethal Company BepInEx QoL mod — drop a waypoint that appears on every modded player's HUD.

**Requires:** BepInExPack (v81 game)

## Use

| Key | Action |
|-----|--------|
| **F8** (`DropKey`) | Place / replace your waypoint at your feet |
| **F7** (`ClearKey`) | Clear your waypoint for everyone |

One waypoint per player. Synced over Unity Netcode to all clients with the mod.

## Indoor / outdoor filter

Waypoints remember whether you were **inside the factory** (`isInsideFactory`) when you dropped them.

- Factory pins only render while you are inside the factory.
- Outdoor / ship pins only render while you are **not** inside the factory.

So a pin dropped in the facility will not clutter outdoor HUD, and vice versa.

## Config (`BepInEx/config/com.benhough.lethal.SharedWaypoints.cfg`)

- `Enabled` — master toggle
- `DropKey` / `ClearKey` — defaults F8 / F7
- `MaxDistance` — hide markers beyond this (default 500 m)
- `ShowOwnWaypoint` — show your own pin (default true)
- `HudScale` — marker text scale (default 1)

## Notes

- Peers without the mod simply won't send or see pins.
- Late joiners receive a resync of existing waypoints from the host.
- All waypoints clear when leaving the moon, entering ship phase, or disconnecting.
