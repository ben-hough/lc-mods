## 1.0.2
- Fix crash on load: patch `GrabbableObject.SetControlTipsForItem` (not SprayPaintItem)
- Awake continues even if Harmony tip patch fails so refill still works

## 1.0.1
- Fix refill key not registering (use Unity Input System instead of legacy Input.GetKeyDown)
- Prefer `currentlyHeldObject` so refill works as a client
- Refill both `sprayCanTank` and battery, then `SyncBatteryServerRpc`
- Add vanilla top-right control tip: `Refill : [R]` when empty
- Log when holding empty / pressing refill for easier debugging

## 1.0.0
- Initial release: refill empty weed killer with configurable key (default R)
