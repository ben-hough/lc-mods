# WeedKillerRefill

Lethal Company BepInEx QoL mod — refill an empty weed killer bottle with a keybind.

## Install
1. Install [BepInEx Pack for Lethal Company](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/).
2. Drop `WeedKillerRefill.dll` into `Lethal Company/BepInEx/plugins/` (beside the game exe, not under `_Data`).

## Usage
- Hold an empty weed killer.
- Top-right tip shows `Refill : [R]` (or your configured key).
- Press **R** (default) to refill tank + battery.

## Config (`BepInEx/config/com.benhough.lethal.WeedKillerRefill.cfg`)
- `RefillKey` — default `R`
- `OnlyWhenEmpty` — default `true`
- `ShowControlTip` — vanilla HUD tip when empty
- `EmptyThreshold` — charge ≤ this counts as empty

## Build
```bash
dotnet build -c Release
```
