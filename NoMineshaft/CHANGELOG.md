## 1.0.5
- Intercept DunGen DungeonGenerator.Generate and swap Mineshaft flow (works on client+host)
- Manual Harmony patches (same approach as door mod)
- Persistent [Watcher] recreated on StartOfRound, Info heartbeat every 5s

## 1.0.4
- VerboseLogging + DungeonTypeWatcher heartbeat (logs currentDungeonType)
- Log when StartOfRound.levels is null instead of silent return

## 1.0.3
- Scrub Mineshaft from all moons on StartOfRound
- Fix early-return that skipped currentDungeonType remap
- Also hook GenerateNewLevelClientRpc (client gen path)
- Always log Prefix hits so client/host logs show activity

## 1.0.2
- Fix Mineshaft removal for v81: always treat interior id 4 as Mineshaft (plus Level3Flow name scan)
- Strip on GenerateNewFloor, ChooseNewRandomMapSeed, and LoadNewLevel
- Remap `currentDungeonType` if Mineshaft was already selected before generation
- Info-level logs so BepInEx console shows strip/reroll activity

## 1.0.1
- Initial public build for Lethal Company v81 GameLibs
