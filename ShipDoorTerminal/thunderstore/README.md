# ShipDoorTerminal

Terminal commands to open, close, or toggle the ship hangar doors.

**Thunderstore:** [MrGlim-ShipDoorTerminal](https://thunderstore.io/c/lethal-company/p/MrGlim/ShipDoorTerminal/)  
**Game:** Lethal Company v81 (and compatible)

## Install

1. Install BepInEx Pack for Lethal Company.
2. Drop `ShipDoorTerminal.dll` into `BepInEx/plugins/` (or install via r2modman / Gale).

## Commands

| Command | Action |
| --- | --- |
| `door` / `doors` | Toggle hangar doors |
| `opendoor` | Open |
| `closedoor` | Close |

On the real help catalog (STORE / BESTIARY / …) you will also see:

```
>DOOR
Toggle the ship hangar doors.
Also: OPENDOOR / CLOSEDOOR
```

The first-boot terminal tip does **not** list DOOR (by design).

## Behaviour notes

- Uses hangar Start/Stop button interact path with RPC fallback
- Reactivates terminal input after a door command (no blank Enter)
- **Blocked while in orbit** (`inShipPhase`) with a clear terminal message

## Config

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | true | Master toggle |
| `VerboseLogging` | true | Trace Parse/OnSubmit/LoadNewNode |

## Build

```bash
dotnet build -c Release
```

## License

MIT — see `LICENSE`.
