# ForceLguRefund

Companion for [malco Lategame_Upgrades](https://thunderstore.io/c/lethal-company/p/malco/Lategame_Upgrades/). Forces the LGU refund UI on so you can sell back an upgrade level.

**Requires:** malco-Lategame_Upgrades (hard dependency)

## Use

1. Install this alongside Late Game Upgrades.
2. Open the LGU store (`lgu` / `lategame store` in the terminal).
3. Unlocked upgrades show the refund action so you can sell a level back.

The mod polls LGU upgrade nodes and sets `Refundable = true`. If a node's refund percentage is 0, it is treated as 100% unless you override it.

## Config (`BepInEx/config/com.benhough.lethal.ForceLguRefund.cfg`)

| Option | Default | Description |
|--------|---------|-------------|
| Enabled | true | Master toggle — force `CustomTerminalNode.Refundable` so the refund UI works as client |
| RefundPercentageOverride | -1 | If >= 0, override each node's refund % (0–100). -1 leaves node values (but 0 becomes 100%) |

## Notes

- Everyone who should see refunds should run this (or at least the client using the store).
- CSync may overwrite LGU's `REFUND_UPGRADES` config; the node `Refundable` flag is the real UI gate.

## Building

`dotnet build -c Release`. Lethal Company, Unity and BepInEx assemblies come from NuGet reference packages (`LethalCompany.GameLibs.Steam`, `BepInEx.Core`, `UnityEngine.Modules`), so no game DLLs live in this repo. `MoreShipUpgrades.dll` (from [malco-Lategame_Upgrades](https://thunderstore.io/c/lethal-company/p/malco/Lategame_Upgrades/)) is not included either: the csproj expects it at `/workspace/refs/MoreShipUpgrades.dll`. Put a copy there or change the `HintPath` to your r2modman/Gale profile's copy.
