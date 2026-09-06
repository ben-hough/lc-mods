# WeedKillerRefill

Refill an empty weed killer can without a trip back to the ship store.

**Thunderstore:** [MrGlim-WeedKillerRefill](https://thunderstore.io/c/lethal-company/p/MrGlim/WeedKillerRefill/)  
**Game:** Lethal Company v81 (and compatible)

## Install

1. Install BepInEx Pack for Lethal Company.
2. Drop `WeedKillerRefill.dll` into `BepInEx/plugins/` (or install via r2modman / Gale).

## Controls

| Input | Action |
| --- | --- |
| **Q / shake** (`ItemInteractLeftRight`) | Primary refill — vanilla blocks normal shake on weed killer |
| **R** (configurable) | Optional alternate refill key |

Tank charge is synced via `ChargeBatteries`.

## Config

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `RefillKey` | R | Optional keybind |
| Other entries | — | See generated cfg after first launch |

## Credits / inspiration

Shake restore path inspired by BetterSprayPaint; charger sync ideas from WeedKillerAdjuster-style approaches.

## Build

```bash
dotnet build -c Release
```

## License

MIT — see `LICENSE`.
