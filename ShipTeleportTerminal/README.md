# ShipTeleportTerminal

Terminal commands to fire the ship teleporter (same as pressing the teleporter button).

## Commands

- `TELEPORT` / `TP` / `BEAM` — normal teleporter (radar-targeted player)
- `ITELEPORT` / `ITP` / `INVERSE` — inverse teleporter (if unlocked; config `AllowInverse`)

Shows up on the main terminal help list as `>TELEPORT`.

Host should run it so ServerRpc paths work cleanly; local button press is used first.
