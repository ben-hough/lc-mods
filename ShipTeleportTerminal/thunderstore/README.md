# ShipTeleportTerminal

Terminal TELEPORT/TP (and ITELEPORT/ITP) to fire the ship teleporter. Host recommended.

**Thunderstore:** [MrGlim-ShipTeleportTerminal](https://thunderstore.io/c/lethal-company/p/MrGlim/ShipTeleportTerminal/)  
**Source:** [lc-mods/ShipTeleportTerminal](https://github.com/ben-hough/lc-mods/tree/main/ShipTeleportTerminal)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Terminal: `teleport` / `tp` for the normal ship teleporter
- Optional `iteleport` / `itp` / `inverse` for inverse teleporter
- Fire teleporters without walking to the button
- Terminal teleports ignore cooldown by default (`RemoveCooldown`)

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-ShipTeleportTerminal** via Thunderstore / r2modman / Gale, or drop `ShipTeleportTerminal.dll` into `BepInEx/plugins/`.

Host should run this so teleporter commands sync for the lobby.

## Config (`BepInEx/config/com.benhough.lethal.ShipTeleportTerminal.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Enable teleport terminal commands |
| `AllowInverse` | true | Allow inverse teleporter commands |
| `RemoveCooldown` | true | Ignore teleporter cooldown for terminal commands |
| `VerboseLogging` | false | Log terminal/teleporter traces |

## Changelog

### 1.0.3
- Terminal teleports ignore the ship teleporter cooldown by default (`RemoveCooldown = true`). Set `RemoveCooldown` to false to restore the vanilla wait.

### 1.0.2
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
