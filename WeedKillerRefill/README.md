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
| **R** (configurable `RefillKey`) | Refill held weed killer |

Control tip shows `Refill : [R]` in the upper-right when holding an empty can.

## Config

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `RefillKey` | R | Refill keybind |
| `AllowKeyRefill` | true | Enable key refill |
| `ShowControlTip` | true | Upper-right tip when empty |
| `OnlyWhenEmpty` | true | Only refill when empty |

## Build

```bash
dotnet build -c Release
```

## License

MIT — see `LICENSE`.
