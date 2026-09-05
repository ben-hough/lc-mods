# ShipDoorTerminal

Lethal Company QoL mod: control the **hangar ship doors** from the terminal.

## Commands

| Command | Action |
| --- | --- |
| `door` / `doors` | Toggle open/closed |
| `opendoor` | Open |
| `closedoor` | Close |

Also accepts `open door`, `close door`, `door open`, `door close`.

## Install

1. Install [BepInEx Pack for Lethal Company](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/).
2. Drop `ShipDoorTerminal.dll` into `BepInEx/plugins/`.

No TerminalApi dependency.

## Notes

- Works when the ship is landed (`shipDoorsEnabled`).
- Refuses to toggle when the doors are overheated.
- Client issues the same door interact the lever uses.

## Build

```bash
dotnet restore
dotnet build -c Release
```

## License

MIT
