# CrewMonitors

Lethal Company BepInEx mod that shows **living players' body-cam feeds** on ship monitors. Extra panels are cloned as needed (up to 8).

## Requirements

- **BepInEx Pack**
- **[Zaggy1024-OpenBodyCams](https://thunderstore.io/c/lethal-company/p/Zaggy1024/OpenBodyCams/)** (hard dependency)

Host **and** clients should install both this mod and OpenBodyCams.

### OpenBodyCams notes

- If you use **LethalLib**, OpenBodyCams may require buying the **antenna** ship upgrade before body cams work.
- **GeneralImprovements** “better monitors” remaps which panel OpenBodyCams claims; this mod skips any renderer already owned by an OpenBodyCams body cam (`AnyBodyCamHasReference`) and never steals `Cube.001` (OBC’s default main panel).

## Behavior

1. After the ship loads, finds unused vanilla screens when possible:
   - Door `SingleScreen`
   - `StartOfRound.insideCameraScreen.mesh`
   - `StartOfRound.securityCameraScreen.mesh`
   - Other `MonitorWall` mesh renderers that are not OpenBodyCams’ main panel
2. Creates one OpenBodyCams body cam per slot via `BodyCam.CreateBodyCam(..., mapRenderer: null)` so each monitor is locked to a player (not synced to the radar map).
3. Clones a template monitor GameObject under `MonitorWall` for any extra slots, laid out in a small grid above/beside the wall.
4. Assigns living, controlled players (`isPlayerControlled && !isPlayerDead`) in stable `playerClientId` order; refreshes about once per second.
5. Powers off / hides unused slots when there are fewer players than monitors.
6. Optional world-space TMP nameplate under each active monitor.

## Config (`BepInEx/config/com.benhough.lethal.CrewMonitors.cfg`)

| Option | Default | Description |
|--------|---------|-------------|
| Enabled | true | Master toggle |
| MaxMonitors | 8 | Slots (1–8) |
| IncludeLocalPlayer | true | Include yourself |
| CloneSpacing | 0.42 | Local spacing for clones |
| RefreshSeconds | 1.0 | Re-assign interval |
| PreferReuseVanillaScreens | true | Reuse unused ship screens first |
| ShowNameplates | true | Player name under monitor |

## Install

Place `CrewMonitors.dll` in `BepInEx/plugins/` (or install via Thunderstore / r2modman with OpenBodyCams).
