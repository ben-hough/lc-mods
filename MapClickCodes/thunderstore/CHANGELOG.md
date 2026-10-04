# Changelog

## 1.0.12

- **Ship monitor hit test matches the picture.** The radar image is only `Cube.001` submesh 1 (material slot 1), not the whole monitor mesh. Clicks were measured on the full box, so the boxes sat offset from the codes. Hover and activate now ray-test that screen submesh and use its real UVs.
- **Lens distortion.** The map camera's `RadarCameraVolume` bends the image (Lens Distortion intensity 0.45) after the codes are drawn. The same HDRP warp is applied to the glass UV before it is compared to a code, so the tip and the click land on the marker you see.
- Bounds / flip / gameplay-viewport guesses are not used on this monitor anymore.

## 1.0.11

- **Non-readable mesh safety** — before any `mesh.vertices` / `mesh.uv` / `mesh.triangles` access (barycentric UV), require `mesh.isReadable`. Ship `mapScreen` mesh `Cube.001` has Read/Write off; the old path spammed Unity Errors every hover/activate frame. Skip silently and keep tip/E via **ss / gvp / peek-cache / bounds / existing MeshCollider textureCoord**.
- **Runtime MeshCollider cook** — do not add a new MeshCollider from a non-readable `MeshFilter.sharedMesh` (same isReadable guard). Prefer any existing collider only.
- One Verbose log per session when a CPU mesh path is skipped for `isReadable=false`.

## 1.0.10

- **Tip → E sync (peek-cache)** — when hover tip resolves a marker, cache `TerminalAccessibleObject` + code + `Time.unscaledTime` (+ NetworkObjectId). On activate (Tick / SwitchRadar / Interact), if cache age ≤ **0.25s** and the object is still valid/active, activate that same object. Log `via=peek-cache code=…`. Guarantees tip code == E even when UV is messy.
- **Gameplay-viewport marker match (`how=gvp`)** — project each active `mapRadarObject` through the monitor world quad into gameplay viewport; pick nearest within ~0.05 viewport of the look sample (flip variants). Prefer over broken screen-space AABB (`ss-uv-no-aabb` with V≈0). High-confidence for peek and activate.
- **SS UV windings** — try multiple corner windings when inverse bilinear collapses to edge V≈0/U≈0; skip weak edge `ss-only` so it cannot block gvp/peek-cache.
- Kept: never suppress SwitchRadar; main map before CrewMonitors soft-dep; terminal-style activate all matching codes + broadcast; tight-ish pads.

## 1.0.9

- **Screen-space monitor UV (root fix)** — `TryScreenSpaceMonitorUv`: build the visible monitor face quad from mesh/renderer bounds (thinnest axis = normal, face toward gameplay camera), project corners + look sample with `gameplayCamera.WorldToViewportPoint`, inverse-bilinear to [0,1] UV. Tag `ss|uRaw|vFlip` etc.; prefer over bounds-axis guesses. Fixes tip code mismatch and activate miss when MeshCollider look-ray UV fails (`mcLook=0` / `bounds-uv-no-aabb` with U≈0).
- Peek and activate share this path (unchanged unified resolve).
- If screen-space succeeds, bounds-only U=0 candidates are not scored as winners; phys/tex/bary kept as bonus when available.
- CrewMonitors panels: same screen-space UV on the panel MeshRenderer.
- Kept: tight hitboxes (actual rect + HitRadius pad); never suppress SwitchRadar; main map before crew; Info on interact miss with `why=` + UV tag.
- **Activate like terminal** — on match, host/local calls `Terminal.CallFunctionInAccessibleTerminalObject(code)` (all TAOs with that code) + `PlayBroadcastCodeEffect`; Info log only when CallFunction actually ran.
- **CrewMonitors soft-dep** — no hard `BepInDependency`; main mapScreen path never requires CrewMonitors; reflection failures → `crew=skip`.

## 1.0.8

- **Unify hover + activate UV** — activate uses the same UV→marker path as the hover tip (`requireMeshUv: false`). Prefer `phys`/`tex`/`bary` when present; otherwise score bounds-orientation candidates (no more hard `uv=bounds-only miss` that dead-ends activate while tip still works).
- **MeshCollider look-ray UV** — when RaycastAll / force hits find the map via `OwnsRenderer` on a non-`MeshCollider`, also `MeshCollider.Raycast` the player look ray (full interact range) for `textureCoord`, adding a runtime non-convex collider if missing. Barycentric uses that surface hit point.
- **Auto orientation/flip** — when only bounds UV is available, score all 3 axis-pair orientations × U/V flips and pick the global best AABB; winning tag logged.
- **Logging** — routine empty / bounds-only misses demoted to Verbose; interact-edge miss with `markers>0` still one Info line with `why=`; successful activate keeps Info (code + UV tag + matchHow).
- Kept: tight hitboxes (no minHalf); never suppress SwitchRadar; main map before CrewMonitors; no Terminal hover spam.

## 1.0.7


- **Hitbox shrink** — removed `minHalf` floor (`Max(0.028, pad*1.25)` ≈ 5.6% viewport boxes). Markers use actual RectTransform viewport AABB + small pad only (default **HitRadius 0.012**). Tiny markers (`w/h < 0.008`) use radius `Max(0.01, pad)`.
- **UV quality gate** — activate matching prefers physics `textureCoord` / barycentric / mesh UV only. If any `phys`/`bary`/`tex` candidates exist, bounds-orientation candidates are not scored. If only bounds candidates exist → activate miss (`uv=bounds-only miss`); hover can still peek via bounds.
- **Radar switch restored** — `SwitchRadarTargetForward` / `SwitchRadarTargetAndSync` **never suppress** vanilla. Prefix still tries high-conf activate, then always `return true`. False aabb must never eat camera cycle; activating a code + advancing radar once is acceptable. `ClickConsumed` unused for radar (InteractTrigger dedupe only).
- **Ray tighten** — `rayViewportMax` 0.1 → **0.04**; loose ray removed from high-confidence activate path.
- Kept: main `mapScreen` before CrewMonitors; hover tip; no Terminal spam.

## 1.0.6

- **Radar switch safety** — `SwitchRadarTargetForward` only suppresses when a **high-confidence** marker match activates (`aabb` / `ray` / `bary` / `phys`). `center` / `centerFallback` never activate and never set `ClickConsumed` (empty-map E always cycles radar). Prefix wraps activate in try/catch and falls through to vanilla on any exception.
- **Main mapScreen first** — resolve `StartOfRound.mapScreen` mesh/mesh2 before CrewMonitors map-feed panels so the big radar is never stolen by a weak/false crew match. CrewMonitors panels still work when the look ray clearly hits those screens (main resolve fails).
- **Terminal-occlusion UV** — under force/near, prefer `Collider.Raycast` (with runtime non-convex `MeshCollider` if missing) → `Bounds.IntersectRay` → **plane face projection + barycentric/texture UV** over `ClosestBounds` (AABB phantom UV≈0,0 always missed). Mesh cast range up to ~10m behind Terminal blockers. Diag logs `mc=0/1` and `via=` / UV source (bary/phys/plane).
- **Hover spam fix** — `forceMapContext` only when looking near the map screen or hovering a Map/Switch/Radar/monitor interact — **not** Terminal. Peek path never Info-logs misses every frame; activate keeps Info logs on interact edge only.
- Reject corner junk UV `(0,0)/(1,1)` from bounds-only candidates instead of logging `tried:N → miss`.

## 1.0.5
- **Hover cursor + tip** — when aimed at a code marker on the main radar or CrewMonitors map panels, overrides the vanilla hover tip with `Activate CODE : [E]` (or `Open CODE : [E]` for big doors) and enables the interact cursor icon so you can tell you are on a clickable code.
- **Activate path hardened** — logs `inCooldown` before invoke; vanilla `CallFunctionFromTerminal` is a silent no-op while cooling down, so we warn and skip claiming success (still play broadcast SFX; powered big doors may `SetDoorToggleLocalClient` only when cooled-out or when `terminalCodeEvent` has zero listeners). **Never double-toggles** doors when the event is already wired.
- **UV reliability** — prefer `MeshCollider.textureCoord` / triangle barycentric UV over bounds-axis guesses; deprioritize/remove noisy edge UVs from ClosestBounds/IntersectRay force paths; tighten nearest-center max to **0.06** and center fallback to **0.05**.
- When `markers=0`, logs why (orbit/ship vs TAOs without active `mapRadarObject` / `mapRadarText`).
- **Activate feedback** — HUD tip (`Map code` / `Activated CODE`) plus terminal broadcast SFX/animator.
- Config: **ShowHoverTip** (default true) to toggle the hover cursor/tip override.
- Peek uses the same resolve + UV + marker match as activate (no cooldown / no side effects).

## 1.0.4

- **Fix InteractTrigger playerBody check** — vanilla calls `InteractTrigger.Interact` with `PlayerControllerB.thisPlayerBody`, not `player.transform`. The prefix now accepts `player.transform`, `thisPlayerBody`, or any transform under the local player, so near-map interact clicks actually run.
- **Center fallback (forceMapContext)** — when precise multi-UV AABB/center/ray matching still misses on SwitchRadar / near-map interact, pick the active `mapRadarObject` closest to the click UV in mapCamera viewport space only if within **0.12** (`via=centerFallback`). Precise matching stays primary.
- **Optional CrewMonitors map-panel clicks** — soft-detect look hits on panels whose material `mainTexture` is a `RenderTexture` named `CrewMapFeedRT_*`, or whose parent name contains `CrewMonitor`. Reflects `CrewMonitors.CrewMonitorManager` slots for that panel's `MapFeedCamera` when the assembly is present; otherwise skips. RT hits without a feed camera fall back to `mapScreen.mapCamera`.
- Always keep Info logs on click / resolve attempts.

## 1.0.3

- **Always Info-log** on interact edge / `SwitchRadarTargetForward` / near-map `InteractTrigger` — resolve failures now emit `MapClick: resolve-fail … via=none` (hits list) instead of staying silent when VerboseLogging is off.
- **Harder hit resolve** — after RaycastAll + Collider.Raycast, prefer `mesh.bounds.IntersectRay`; under `forceMapContext`, fall back to closest point on bounds so SwitchRadar almost never fails to produce a hitPoint when the mesh exists. Plane projection kept as last resort. Log `via=` path that won.
- **Multi-UV marker match** — score across ~12 UV candidates (3 local axis-pair orientations × U/V flips, plus MeshCollider texcoord variants). Markers scored by padded AABB / nearest-center **and** `mapCamera.ViewportPointToRay` → world distance to the marker (≤4m or viewport ≤0.1). Global best wins; no single-marker auto-fire.
- **Interact detection** — `IngamePlayerSettings` Interact action, `playerActions.Movement.Interact`, then `Keyboard.current.eKey` hangar fallback. Tick forces map context when looking near the screen or `hoveringOverTrigger` is set.
- **Optional `InteractTrigger.Interact` prefix** — when local player interact fires while looking near the map mesh, try activate with `forceMapContext` alongside vanilla (does not block other triggers).

## 1.0.2

- **Hit detection rewrite** — `Physics.RaycastAll` / NonAlloc prefers a hit owned by `map.mesh` / `mesh2` even when another collider sits in front; also tries `Collider.Raycast` on mesh colliders directly.
- **Plane fallback** — if the mesh is missed but the player is in range (or vanilla `SwitchRadarTargetForward` fired), project the look ray onto the screen plane / bounds and derive UV from local mesh axes.
- **SwitchRadar force context** — map-switch prefix always attempts UV→marker (`forceMapContext`), so a blocker collider cannot skip click-to-code.
- **More forgiving markers** — raised minimum hit half-size so old `HitRadius=0.012` configs still work; also nearest-center match within `max(0.06, HitRadius*3)`.
- **Sparse always-on Info logs** on every map click attempt (top RaycastAll colliders, UV, flip, marker count, hit/miss) so hosts can verify without enabling VerboseLogging.
- Still **main `mapScreen` only** — never CrewMonitors clones. HostModGate client→host activate unchanged.

## 1.0.1

- **Client map clicks** now send a host-authoritative activate request (`NetworkObjectId` + object code); the host runs `CallFunctionFromTerminal`.
- **HostModGate** retries hello sync every ~2s while connected until the host hello arrives; re-registers on `StartOfRound` / `NetworkManager.Initialize`.
- Clear warning logs when interact is blocked by the gate (waiting for hello vs host missing/disabled).
- Default **HitRadius** raised to `0.022`; tiny door marker rects get a larger minimum hit size.
- Still main radar only (`mapScreen.mesh` / `mesh2`); CrewMonitors clones unchanged.

## 1.0.0

- Initial release.
- Click terminal code markers on the main ship radar to activate `CallFunctionFromTerminal`.
- Precise UV / viewport hit test against `mapRadarObject` RectTransforms.
- Suppresses vanilla radar target switch only when a marker is hit.
- Host-gated networking (features active only when host has the mod enabled).
- Config: Enabled, VerboseLogging, HitRadius, FlipUvV, InteractRange.
