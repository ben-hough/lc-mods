# NoMineshaft

Lethal Company BepInEx mod that removes **Mineshaft** interiors from dungeon rotation.

**Thunderstore:** [MrGlim-NoMineshaft](https://thunderstore.io/c/lethal-company/p/MrGlim/NoMineshaft/)  
**Game:** Lethal Company v81 (and compatible)

## Install

1. Install [BepInEx Pack for Lethal Company](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/).
2. Drop `NoMineshaft.dll` into `BepInEx/plugins/` (or install via r2modman / Gale).
3. **Host must run this mod** — dungeon selection is decided on the host.

## What it does

- Scrubs Mineshaft (dungeon id `4` / Level3Flow) from moon dungeon lists
- Intercepts floor generation and keeps a lightweight watcher so Mineshaft does not slip back in
- Works for host; clients benefit when the host has it

## Config (`BepInEx/config`)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `VerboseLogging` | true | Extra scrub/watcher logs |

## Troubleshooting

- Still seeing Mineshaft? Confirm the **lobby host** has `NoMineshaft` loaded (`LogOutput.log` should show `NoMineshaft v1.0.6 loaded` and scrub lines).
- Zero runtime logs after load can mean an old broken build — use **1.0.6+**.

## Build

```bash
dotnet build -c Release
```

Output: `bin/Release/netstandard2.1/NoMineshaft.dll`

## License

MIT — see `LICENSE`.
