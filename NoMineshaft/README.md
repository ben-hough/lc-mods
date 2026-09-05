# NoMineshaft

Lethal Company QoL mod that removes **Mineshaft interiors** from dungeon rotation.

> Note: Mineshaft is an *interior* type (Factory / Manor / Mineshaft), not a moon on the route list.

## Install

1. Install [BepInEx Pack for Lethal Company](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/) (`BepInEx-BepInExPack`).
2. Build this project (see below) or grab `NoMineshaft.dll` from a release.
3. Drop `NoMineshaft.dll` into `Lethal Company/BepInEx/plugins/`.
4. Launch the game once. Config appears at `BepInEx/config/com.benhough.lethal.NoMineshaft.cfg`.

## Multiplayer

Changes run during host-side floor generation. **Only the host needs the mod** for Mineshaft removal to apply. Clients should still use matching interior-related mods when possible to avoid desyncs with other dungeon mods.

## Config

| Key | Default | Description |
| --- | --- | --- |
| `General.Enabled` | `true` | Turn Mineshaft removal on/off |

## Build

Requires [.NET SDK](https://dotnet.microsoft.com/download) 6+.

```bash
dotnet restore
dotnet build -c Release
```

Output: `bin/Release/netstandard2.1/NoMineshaft.dll`

References stripped/publicized game assemblies via [LethalCompany.GameLibs.Steam](https://www.nuget.org/packages/LethalCompany.GameLibs.Steam) (no local game install required to compile). Mineshaft is resolved by DunGen flow name `Level3Flow`, with vanilla id `4` as fallback.

## Thunderstore packaging

`thunderstore/` contains a starter `manifest.json`. Add a 256×256 `icon.png`, then zip:

- `manifest.json`
- `README.md`
- `icon.png`
- `NoMineshaft.dll` (built Release)

Dependency string: `BepInEx-BepInExPack-5.4.2100`

## Credits

Approach inspired by community interior-filter patterns (e.g. MIT-licensed [RemoveTheAnnoying](https://github.com/trevorswan11/RemoveTheAnnoying)). Implementation here is original and scoped to Mineshaft only.

## License

MIT
