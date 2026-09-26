# ForceLguRefund

Force Late Game Upgrades refund UI on so you can sell back upgrade levels. Companion for malco Lategame_Upgrades.

**Thunderstore:** [MrGlim-ForceLguRefund](https://thunderstore.io/c/lethal-company/p/MrGlim/ForceLguRefund/)  
**Source:** [lc-force-lgu-refund](https://github.com/ben-hough/lc-force-lgu-refund)  
**Game:** Lethal Company (BepInEx)

> **Networking:** Host should install this mod so gameplay changes sync for the lobby.

## Features

- Forces LGU terminal nodes to be refundable
- Optional override for refund percentage
- Companion tweak for malco Late Game Upgrades

## Install

1. Install [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) for Lethal Company.
2. Install dependency **malco-Lategame_Upgrades** from Thunderstore.
3. Install **MrGlim-ForceLguRefund** via Thunderstore / r2modman / Gale, or drop `ForceLguRefund.dll` into `BepInEx/plugins/`.

Requires malco-Lategame_Upgrades. Host/clients should match for shop UI consistency.

## Config (`BepInEx/config/com.benhough.lethal.ForceLguRefund.cfg`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Force Refundable=true on LGU nodes |
| `RefundPercentageOverride` | -1 | >=0 overrides %; -1 keeps node values |

## Changelog

### 1.0.1
- Packaging refresh: professional icon, categories (incl. AI Generated), polished README.

## License

MIT
