# SprayPatterns (MrGlim)

Overwatch-style **spray pattern stamps** for the Lethal Company spray paint can: radial select, ghost preview, rotate, then stamp with the normal spray input. Vanilla free-paint stays available via a **Vanilla / Free** radial slot.

## Features

- **Radial menu** (default **V**) while holding a spray paint can — pick a pattern with the mouse, click to confirm.
- **Patterns** (procedural art, no external textures):
  - **Vanilla / Free** — normal continuous spray paint (vanilla behavior)
  - **Arrow** — pointing marker
  - **MAIN** / **FIRE** — arrow with text label
  - **Dry** — looted / empty
  - **Juicy** — worth checking / more loot
  - **Skull** — hazard
  - **Sun** — sunshine / clear
- **Ghost preview** of the selected stamp where it will land (raycast like vanilla spray).
- **Rotate** before stamping: mouse wheel, or **[** / **]** (config bindable). Q is left alone for vanilla shake.
- **Primary fire (LMB)** stamps the selected pattern (synced via Netcode named messages). Tank + shake still consumed.
- **Weed killer** bottles are never intercepted.
- Empty tank / needs-shake feedback still plays when you try to stamp with an empty or unshaken can.

## Controls

| Action | Default |
|--------|---------|
| Open / close radial | **V** |
| Select pattern | Aim with mouse, **LMB** click |
| Stamp selected pattern | **LMB** (spray use) while a non-Vanilla pattern is selected |
| Vanilla free-paint | Select **Vanilla** on the radial, then LMB as usual |
| Rotate stamp | Mouse wheel, **[** / **]** |
| Shake can | **Q** (vanilla, unchanged) |

## How vanilla spray is preserved

1. Select **Vanilla / Free** on the radial (default on load).
2. LMB uses stock `SprayPaintItem` free-paint (`TrySpraying` → `AddSprayPaintLocal` → ServerRpc/ClientRpc).
3. Pattern mode only replaces activate when a non-Vanilla pattern is selected; weed killer is never patched for stamping.

## Config (`BepInEx/config/com.benhough.lethal.SprayPatterns.cfg`)

| Key | Default | Notes |
|-----|---------|--------|
| Enabled | true | Master toggle |
| RadialKey | V | Toggle radial |
| RotateLeftKey / RotateRightKey | [ / ] | Keyboard rotate |
| InvertScroll | false | Flip wheel direction |
| RotateStepDegrees | 15 | Per step |
| PreviewOpacity | 0.45 | Ghost alpha |
| StampSize | 0.85 | World meters |
| StampTankCost / StampShakeCost | 0.04 / 0.12 | Per stamp |
| StampCooldown | 0.35 | Seconds between stamps |
| Patterns.Enable* | true | Per-pattern toggles |

## Install

Drop `SprayPatterns.dll` into `BepInEx/plugins/` (or a subfolder such as `MrGlim-SprayPatterns/`). Requires **BepInEx 5**.

## Credits

MrGlim / Ben Hough — GUID `com.benhough.lethal.SprayPatterns`.


## AI disclosure

This mod was made with the help of generative AI. The code and this README were produced with an AI coding agent, directed by the author (MrGlim / Ben Hough). The Thunderstore package is listed in the **AI Generated** category.
