# EmotePlaylist

Companion for FlipMods TooManyEmotes. Turns your 8 quick-slot remotes into a looping playlist.

**Requires:** FlipMods-TooManyEmotes

## Use

1. Assign emotes to the quick slots (wheel remotes).
2. Press **F10** (configurable `ListKey`) to show/hide the on-screen playlist panel (slot names 1–8). With `ShowListOnStart` (default on), the list appears briefly when you first spawn this session, plus a tip.
3. Press **F9** (configurable `PlaylistKey`) to start. Press again to stop.
4. Each emote plays for `SecondsPerEmote` (default 120, clamped 60–300), then the next, then loops. While running, the list highlights the current slot and remaining seconds.
5. Another player looks at you and presses **E**. If they also have this mod, they join the whole list and the same cycle clock (not just the current dance).

Optional `PlaylistOverride`: comma-separated emote names instead of the quick slots.

Everyone who should follow the playlist needs this mod installed.

## Building

`dotnet build -c Release`. Lethal Company, Unity and BepInEx assemblies come from NuGet reference packages (`LethalCompany.GameLibs.Steam`, `BepInEx.Core`, `UnityEngine.Modules`), so no game DLLs live in this repo. `TooManyEmotes.dll` (from [FlipMods-TooManyEmotes](https://thunderstore.io/c/lethal-company/p/FlipMods/TooManyEmotes/)) is not included either: the csproj expects it at `/workspace/refs/TooManyEmotes.dll`. Put a copy there or change the `HintPath` to your r2modman/Gale profile's copy.
