# ShipBeacon

Lethal Company QoL mod that shows a simple **outdoor ship beacon** on your HUD: an arrow + optional distance pointing toward the ship.

Hidden while you are inside the facility or in the ship hangar.

## Install

1. Install [BepInEx Pack for Lethal Company](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/).
2. Build this project or use a release DLL.
3. Drop `ShipBeacon.dll` into `Lethal Company/BepInEx/plugins/`.

## Config (`com.benhough.lethal.ShipBeacon.cfg`)

| Key | Default | Description |
| --- | --- | --- |
| `Enabled` | `true` | Toggle the HUD |
| `ShowDistance` | `true` | Show meters to ship |
| `HudScale` | `1.0` | Text scale |
| `VerticalOffset` | `0.12` | Vertical placement (0 = top, 1 = bottom) |

## Build

```bash
dotnet restore
dotnet build -c Release
```

Output: `bin/Release/netstandard2.1/ShipBeacon.dll`

## Notes

- Client-side HUD only — each player who wants it should install it.
- Ship position uses `StartOfRound.elevatorTransform` (ship body).

## License

MIT
