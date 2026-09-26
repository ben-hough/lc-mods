# Releasing

This repository holds several mods, one per top-level folder. Each mod is built, versioned and released independently.

## Build

Build a mod from its own folder, e.g. `cd <ModFolder> && dotnet build -c Release` (see the mod's README for its exact project path and packaging steps). Build output (`bin/`, `obj/`, `*.dll`) and release zips are git-ignored and must never be committed.

## Tags

Release tags use the scheme `<ModFolder>-vX.Y.Z`, for example `CadaverWilt-v1.0.2`. The folder name is the mod's project name exactly as it appears at the repository root.

```
git tag -a CadaverWilt-v1.0.2 -m "CadaverWilt 1.0.2"
git push origin CadaverWilt-v1.0.2
```

Bump the version in the mod's own manifest / project files first, commit, then tag that commit. A GitHub release, if made, uses the same tag name and attaches that mod's package only.
