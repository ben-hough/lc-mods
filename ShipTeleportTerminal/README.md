# ShipTeleportTerminal

Terminal commands to fire the ship teleporter (same as pressing the teleporter button).

## Commands

- `TELEPORT` / `TP` / `BEAM` — normal teleporter (radar-targeted player)
- `ITELEPORT` / `ITP` / `INVERSE` — inverse teleporter (if unlocked; config `AllowInverse`)

Shows up on the main terminal help list as `>TELEPORT`.

Host should run it so ServerRpc paths work cleanly; local button press is used first.

By default, terminal teleports ignore the ship teleporter cooldown (`RemoveCooldown = true`). Set it to false to restore the vanilla wait.

## Config (`BepInEx/config/com.benhough.lethal.ShipTeleportTerminal.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Enable teleport terminal commands |
| `AllowInverse` | true | Allow inverse teleporter commands |
| `RemoveCooldown` | true | Ignore teleporter cooldown for terminal commands |
| `VerboseLogging` | false | Log terminal/teleporter traces |
