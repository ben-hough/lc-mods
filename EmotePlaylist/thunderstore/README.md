# EmotePlaylist

Loop TooManyEmotes quick-slot remotes on a shared cycle. Press E to sync into the playlist.

**Thunderstore:** [MrGlim-EmotePlaylist](https://thunderstore.io/c/lethal-company/p/MrGlim/EmotePlaylist/)  
**Source:** [lc-emote-playlist](https://github.com/ben-hough/lc-emote-playlist)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Requires TooManyEmotes; install on clients who want to join the playlist.

## Features

- Cycles TooManyEmotes quick-slot remotes on a shared timer
- F9 starts/stops playlist; F10 lists slots (configurable)
- Press E to join the current playlist sync
- Optional override string for custom slot order

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install dependency **FlipMods-TooManyEmotes** from Thunderstore.
3. Install **MrGlim-EmotePlaylist** via Thunderstore / r2modman / Gale, or drop `EmotePlaylist.dll` into `BepInEx/plugins/`.

Requires FlipMods-TooManyEmotes. Clients who want to dance need the mod.

## Config (`BepInEx/config/com.benhough.lethal.EmotePlaylist.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `PlaylistKey` | F9 | Start/stop playlist |
| `ListKey` | F10 | List quick-slot remotes |
| `ShowListOnStart` | true | Print list when playlist starts |
| `SecondsPerEmote` | 120 | Seconds per emote (≈1–5 min) |
| `PlaylistOverride` | "" | Optional comma-separated slot override |

## Changelog
- **1.0.3** — Host-gated networking where applicable, new icon, Thunderstore categories (incl. AI Generated), polished README.


### 1.0.2
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
