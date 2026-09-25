## 1.0.4
- Shared waypoint pins now appear on the ship radar / map video feeds (same world markers as door codes under `mapCameraStationaryUI`). Works with CrewMonitors map feeds that copy `mapCamera` culling — no soft dependency required.
- HUD and networking behavior unchanged.

## 1.0.3
- Each player can keep 1 outdoor and 1 indoor waypoint; dropping one zone no longer clears the other. Clear removes only the current zone pin.

## 1.0.2
- Fix OverflowException on client sync-request (FastBufferWriter was 8 bytes for 1+8 write)

## 1.0.1
- Fixed top-right HUD panel instead of world-tracked floating markers.
- Lines show name, distance, and relative bearing.
