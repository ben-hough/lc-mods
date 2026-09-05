# WeedKillerRefill

Lethal Company QoL mod: when a held **weed killer** can is empty, press a key to refill it.

## Why not Q?

In Lethal Company, **Q is typically Drop**. This mod defaults to **R** (same idea as reload / `ReloadBatteries`). Change `RefillKey` in config if you want another bind, including Q.

## Install

1. Install [BepInEx Pack for Lethal Company](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/).
2. Drop `WeedKillerRefill.dll` into `BepInEx/plugins/`.

## Config (`com.benhough.lethal.WeedKillerRefill.cfg`)

| Key | Default | Description |
| --- | --- | --- |
| `Enabled` | `true` | Master toggle |
| `RefillKey` | `R` | Key to refill |
| `OnlyWhenEmpty` | `true` | Only refill when tank is empty |
| `EmptyThreshold` | `0.02` | Charge ≤ this counts as empty |
| `ShowHint` | `true` | On-screen hint when empty / after refill |

## Build

```bash
dotnet restore
dotnet build -c Release
```

## Notes

- Client-side for the local held item. In multiplayer, each client with the mod can refill their own held can.
- Related: ship-charger style refill exists in community mods like WeedKillerAdjuster; this one is a direct key action instead.

## License

MIT
