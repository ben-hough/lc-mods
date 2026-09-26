# ShipStarterKit

Lethal Company BepInEx mod that **pre-supplies the ship** with configurable store equipment on the **first load of each save file**. Items appear in **separate piles per type** on the ship floor.

**Thunderstore namespace:** MrGlim  
**Author:** Ben Hough (MrGlim)  
**GUID:** `com.benhough.lethal.ShipStarterKit`

## Requirements

- [BepInEx Pack](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/)

## Behavior

1. **Host-only spawn** — the host instantiates networked `GrabbableObject`s (same pattern as vanilla `LoadShipGrabbableItems` / dropship delivery) so clients see the piles.
2. **First load per save only** — an ES3 flag `ShipStarterKit_Supplied` is written on `GameNetworkManager.currentSaveFileName` (e.g. `LCSaveFile1`). Reloading that save or returning from moons does **not** re-spawn. Starting a **new game** on the slot clears the flag via `ResetSavedGameValues`.
3. **Config per store item** — defaults of **10** for belt bags, pro flashlights, jetpacks, lockpickers, spray paint, weed killer, and shovels. All other buyable store equipment is listed at **0** (raise in config as desired).
4. **HostModGate** (`MrGlim.ShipStarterKit`) — advantage QoL; features follow host enable state.

## Config (`BepInEx/config/com.benhough.lethal.ShipStarterKit.cfg`)

| Section | Key | Default | Notes |
|---------|-----|---------|-------|
| General | Enabled | true | Master toggle |
| General | VerboseLogging | false | Match / pile logs |
| General | SpawnOncePerSave | true | Keep true unless testing |
| ItemCounts | BeltBag, ProFlashlight, Jetpack, Lockpicker, SprayPaint, WeedKiller, Shovel | 10 each | Flexible name match |
| ItemCounts | *(other buyables)* | 0 | Auto-bound after Terminal loads |

## First-load gate (detail)

- Hook: `StartOfRound.Start` Postfix on **server** (after vanilla `LoadShipGrabbableItems`).
- `StartOfRound` persists across moon trips, so this does **not** fire every landing.
- Persistence: ES3 on the current save file name; file fallback under `BepInEx/config/ShipStarterKit/` if ES3 reflection fails.

## Spawn API

```
Instantiate(item.spawnPrefab, pos, identity, elevatorTransform)
→ GrabbableObject setup (fallTime=0, isInShipRoom, scrapPersistedThroughRounds)
→ NetworkObject.Spawn(false)
```

## License

MIT — Ben Hough, 2026


## AI disclosure

This mod was made with the help of generative AI. The code and this README were produced with an AI coding agent, directed by the author (MrGlim / Ben Hough). The Thunderstore package is listed in the **AI Generated** category.
