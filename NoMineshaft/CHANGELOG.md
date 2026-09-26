# Changelog

## 1.0.6
- Fixed Unity fake-null `Plugin.Instance` check that silently disabled all patches and the watcher
- `GenerateNewFloor` postfix forces flow swap away from Mineshaft
- DunGen intercept + LateUpdate watcher with heartbeat logs
- Scrubs dungeon id 4 / Level3Flow from moon lists on `StartOfRound.Start`

## 1.0.5
- Attempted generate-time DunGen flow swap and persistent watcher (superseded by 1.0.6 Instance fix)

## 1.0.1 – 1.0.4
- Early scrub / mapping iterations for v81
