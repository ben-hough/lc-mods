# MapClickCodes

Click a **terminal code marker** (door / turret / mine / trap / etc.) on the **main ship radar screen** to activate that object's code — same effect as typing the code into the terminal.

## Features

- **Aligned click boxes (1.0.12)** — ship radar hits use the screen submesh UV plus the map camera's lens distortion, so the hover tip and E land on the code you see. The old full-monitor box guess is not used on that screen.
- **Tip → E sync (1.0.10)** — hover tip caches the resolved marker for 0.25s; E activates that same object (`via=peek-cache`) so tip code always matches activate even when UV is messy.
- **Gameplay-viewport match (1.0.10)** — markers projected through the monitor glass into gameplay viewport (`how=gvp`); preferred over broken screen-space AABB (V≈0 / `ss-uv-no-aabb`).
- **Screen-space UV (1.0.9+)** — UV from the visible monitor face (multi-winding inverse bilinear). MeshCollider UV kept as bonus.
- **Hover tip == click target (1.0.8+)** — activate shares resolve with hover; gvp / peek-cache / mesh UV preferred over bounds guesses.
- **MeshCollider look-ray UV (1.0.8)** — RaycastAll map hits also MeshCollider-raycast the look ray for `textureCoord` (runtime collider if needed; bonus when it works).
- **Hitbox shrink (1.0.7)** — actual marker AABB + small pad (default HitRadius **0.012**); no minHalf inflate.
- **Radar never suppressed (1.0.7)** — vanilla `SwitchRadarTargetForward` always runs after a click attempt (code activate + radar advance OK).
- **Main map first (1.0.6)** — `StartOfRound.mapScreen` resolves before CrewMonitors map-feed panels; crew panels still work when clearly aimed at.
- **Terminal-occlusion UV (1.0.6)** — plane/barycentric/`MeshCollider` preferred over ClosestBounds phantom UV; no hover miss spam on Terminal.
- **Hover feedback (1.0.5)** — cursor icon + `Activate/Open CODE : [E]` tip when aimed at a marker; HUD tip on activate.
- **Activate harden (1.0.5)** — logs `inCooldown`; no silent false success; no double door toggle; mesh-first UV.
- **Main radar fully standalone (1.0.9)** — `StartOfRound.mapScreen` works with **no CrewMonitors installed** (no hard dependency / no TypeLoadException). CrewMonitors is an **optional soft-dep** for map-feed panel clicks only (reflection; `crew=skip` when absent).
- **Robust hits (1.0.3–1.0.9)** — `RaycastAll` prefers the real map mesh; screen-space UV primary; MeshCollider look-ray UV as bonus; falls back to `Collider.Raycast`, `Bounds.IntersectRay`, plane, closest-on-bounds.
- **Precise markers** — UV → `mapCamera` viewport, then actual AABB (+ small pad). Nearest-center / tight ray are hover-only / non-activating. Empty map areas do nothing.
- **Vanilla-friendly** — radar target switching is **never** blocked; high-confidence mesh-UV hits still activate the code alongside the vanilla cycle.
- **Host-gated** — click-to-code is active only when the lobby host has this mod enabled (same fairness pattern as other MrGlim advantage QoL mods). Clients retry hello sync until the host acknowledges.
- **Host-authoritative activate** — non-host players hit-test locally, then the host runs `CallFunctionFromTerminal` (local SFX on the clicker is fine).
- **SFX** — plays the terminal code-broadcast animation/sound when available.
- **Diagnostics** — Info on successful activate and on interact-edge miss when markers>0; routine empty misses are Verbose-only. Enable VerboseLogging for UV/match detail.

## How to use

1. Stand in the ship and look at the **large main map / radar monitor** (or a CrewMonitors panel showing a **map feed**).
2. Aim at a green/red code box on the radar feed — the cursor tip should change to **Activate/Open CODE : [E]** when you are on a marker.
3. Press **Interact (E)** — that object's terminal code runs (`CallFunctionFromTerminal`), with a short HUD tip confirming activation.

## Config (`BepInEx/config/com.benhough.lethal.MapClickCodes.cfg`)

| Key | Default | Notes |
|-----|---------|--------|
| Enabled | true | Master toggle |
| VerboseLogging | false | Extra UV / match traces |
| HitRadius | 0.012 | Extra UV padding around marker rects (small pad only; no minHalf inflate) |
| FlipUvV | true | Prefer V-flip when ranking UV candidates (all flip/orientation variants are still tried) |
| InteractRange | 4.5 | Max ray distance to the screen mesh |
| ShowHoverTip | true | Cursor icon + tip when aimed at a code marker |

## Networking

Non-host clients with the mod send a named Netcode activate request to the host after a successful marker hit. The host validates the `TerminalAccessibleObject` (`NetworkObjectId`, with object-code fallback) and calls `CallFunctionFromTerminal()`. Door/turret/mine behaviour continues to use the object's own UnityEvents / ServerRpcs, so activation syncs like typing a code.

**Both host and clients should install** this mod from Thunderstore so everyone can click markers. Host must enable it for the lobby. Clients without the mod still see synced activations when someone else clicks, but cannot click themselves.

This is a convenience over walking to the terminal — treat it as an advantage feature for your lobby rules.

## Dependencies

- BepInExPack only (no OpenBodyCams).
- **CrewMonitors** — optional. Main mapScreen click-to-code works without it.

## Credits

Ben Hough (MrGlim) — Thunderstore team **MrGlim**.
