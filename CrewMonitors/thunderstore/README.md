# CrewMonitors

Lethal Company BepInEx mod that adds a **fixed 4-panel crew monitor stack** on the ship wall. Each panel shows OpenBodyCams body cams (and map / external views via the cycle button).

**Thunderstore:** [MrGlim-CrewMonitors](https://thunderstore.io/c/lethal-company/p/MrGlim/CrewMonitors/)  
**Source:** [lc-crew-monitors](https://github.com/ben-hough/lc-crew-monitors)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Install with OpenBodyCams on **host and clients** for monitor feeds. This is display QoL on top of OpenBodyCams — no extra host-only gate beyond OBC itself.

## Requirements

- **[BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/)**
- **[Zaggy1024-OpenBodyCams](https://thunderstore.io/c/lethal-company/p/Zaggy1024/OpenBodyCams/)** (hard dependency)

### OpenBodyCams notes

- If you use **LethalLib**, OpenBodyCams may require buying the **antenna** ship upgrade before body cams work.
- **GeneralImprovements** “better monitors” remaps which panel OpenBodyCams claims; this mod skips any renderer already owned by an OpenBodyCams body cam and never steals `Cube.001` (OBC’s default main panel).
- This mod does **not** change OpenBodyCams quality settings (e.g. `HorizontalResolution`); configure those in the OpenBodyCams profile.
- Screen look matches OpenBodyCams **MonitorEmissiveColor**. **ScreenBrightness** only scales night-vision fill on crew body cams (keeps interior feeds from going milky).

## Behavior

1. **Always 4 panels** (default `MaxMonitors=4`, vertical stack). Living players fill from the **bottom up**. Empty slots stay **Off** (black). There is **no round-robin** duplication past 4 players.
2. By default (`PreferReuseVanillaScreens=false`) leaves vanilla door/internal cams alone and clones **SingleScreen** panels on the main monitor wall (`Cube.001`), flush via localBounds depth + `WallInset`.
3. **Cycle button** (bottom-right of each panel): grey bezel cylinder with a white face. Uses the vanilla **hand** cursor. Cycle order per panel:
   - **Off** → player0 **body** → player0 **map** → player1 body → player1 map → … → **External** → Off
4. **Hover a panel**: transparent cursor (no hand icon clutter). Tooltip shows the player name (and “Map” when on map feed). Tooltip is **hidden** for Off / External.
5. **Click a panel**: focuses the ship main radar / teleporter target on that panel’s assigned player (`mapScreen.SwitchRadarTargetAndSync`).
6. Optional world-space TMP nameplates under each monitor (`ShowNameplates`).
7. `AlwaysShowMonitors=true` keeps all panel meshes visible; empty / Off slots stay black rather than hiding the frame.

## Config (`BepInEx/config/com.benhough.lethal.CrewMonitors.cfg`)

| Option | Default | Description |
|--------|---------|-------------|
| Enabled | true | Master toggle |
| MaxMonitors | 4 | Slots (1–8); default stack is 4 |
| IncludeLocalPlayer | true | Include yourself in assignments |
| CloneSpacing | 0.42 | Local spacing on the Cube.001 wall plane |
| CloneScaleFactor | 0.60 | Panel size knob (`localScale = SingleScreen.localScale * (CloneScaleFactor * 2.0 / 0.6)`; 0.6 ≈ prior good tile size) |
| WallInset | 0.55 | Flush inset into the wall along the thin depth axis |
| GridHorizontalOffset | 3.68 | Grid shift along wall-plane horizontal |
| GridVerticalOffset | -0.45 | Grid shift along wall-plane vertical |
| GridYawDegrees | 20 | Panel rotation around local Z (degrees) |
| GridColumns | 1 | Columns in the clone grid (1 = vertical stack) |
| RefreshSeconds | 1.0 | Re-assign interval (seconds) |
| PreferReuseVanillaScreens | false | Reuse whitelisted unused ship screens first (default: clone grid only) |
| ShowNameplates | true | Player name under monitor |
| AlwaysShowMonitors | true | Keep all panels visible; empty slots Off (black), no round-robin |
| ScreenBrightness | 0.2 | Night-vision fill scale only (1.0 = stock OBC); emissive uses OBC MonitorEmissiveColor |

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install dependency **Zaggy1024-OpenBodyCams** from Thunderstore.
3. Install **MrGlim-CrewMonitors** via Thunderstore / r2modman / Gale, or drop `CrewMonitors.dll` into `BepInEx/plugins/`.

## License

MIT
