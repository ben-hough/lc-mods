# DoorTeleporter

Terminal DOORPORT/DTP teleports you in front of a random main entrance or fire exit. Host recommended.

**Thunderstore:** [MrGlim-DoorTeleporter](https://thunderstore.io/c/lethal-company/p/MrGlim/DoorTeleporter/)  
**Source:** [lc-door-teleporter](https://github.com/ben-hough/lc-door-teleporter)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Terminal commands: `doorport` / `dtp` / `exitport` / `fireexit`
- Picks a random main entrance or fire exit
- Stands you slightly in front of the door (configurable offset)

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install **MrGlim-DoorTeleporter** via Thunderstore / r2modman / Gale, or drop `DoorTeleporter.dll` into `BepInEx/plugins/`.

Host should run this for reliable terminal teleport sync.

## Config (`BepInEx/config/com.benhough.lethal.DoorTeleporter.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `IncludeMainEntrance` | true | Allow main facility entrance |
| `IncludeFireExits` | true | Allow fire exits |
| `StandBackMeters` | 0.35 | Nudge back from entrance point |
| `VerboseLogging` | false | Extra logs |

## Changelog
- **1.0.2** — Host-gated networking where applicable, new icon, Thunderstore categories (incl. AI Generated), polished README.


### 1.0.1
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
