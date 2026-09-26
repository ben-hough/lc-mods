# ShipLightsTerminal

Terminal commands to turn the ship lights on, off, or toggle them.

**Website:** [github.com/ben-hough/lc-mods/tree/main/ShipLightsTerminal](https://github.com/ben-hough/lc-mods/tree/main/ShipLightsTerminal)  
**Game:** Lethal Company v81 (and compatible)

## Install

1. Install BepInEx Pack for Lethal Company.
2. Drop `ShipLightsTerminal.dll` into `BepInEx/plugins/` (or install via r2modman / Gale).

## Commands

| Command | Action |
| --- | --- |
| `lights` / `light` / `ship lights` | Toggle ship lights |
| `lightson` / `lights on` / `turn on lights` | Turn on |
| `lightsoff` / `lights off` / `turn off lights` | Turn off |
| `togglelights` / `toggle lights` | Toggle |

On the real help catalog (STORE / BESTIARY / …) you will also see:

```
>LIGHTS
Toggle ship lights on/off.
Also: LIGHTSON / LIGHTSOFF
```

The first-boot terminal tip does **not** list LIGHTS (by design).

## Behaviour notes

- Calls `ShipLights.SetShipLightsServerRpc` so the state syncs (`RequireOwnership=false`)
- Falls back to `Object.FindObjectOfType<ShipLights>()` if `StartOfRound.shipRoomLights` is null
- Reactivates terminal input after a lights command (no blank Enter)

## Config

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `VerboseLogging` | false | Trace Parse/OnSubmit/LoadNewNode |

## Build

```bash
dotnet build -c Release
```

## License

MIT — see `LICENSE`.
