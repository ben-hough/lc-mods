# Changelog

## 1.0.32

- Per-player map feeds use dedicated follower cameras + private RenderTextures (no longer share `mapScreen` RT / radar target). Multiple panels can show different players' maps; main radar / click-to-focus unchanged.

## 1.0.31

- Thunderstore polish: README/config table match live defaults; packaging synced to 1.0.31.

## 1.0.24 – 1.0.30

Feature arc since the early cycle-button builds:

- **Fixed 4-panel stack** with bottom-up fill; empty slots Off; no round-robin past MaxMonitors.
- **Cycle order**: Off → each living player body → that player’s map radar → … → External → Off.
- **Cycle button** restyled as a grey bezel cylinder with white face (bottom-right); hand cursor on buttons.
- **Panel hover**: transparent cursor; player-name tooltip (hidden for Off / External).
- **Click panel** focuses main radar / teleport target.
- Screen emissive matches OpenBodyCams **MonitorEmissiveColor**; **ScreenBrightness** only scales night-vision fill (default 0.2).
- Placement defaults tuned (`WallInset`, grid offsets, `CloneScaleFactor`, yaw).
- Map-feed restore so body-cam RenderTexture returns correctly after map view.

## 1.0.20

- Darker default `ScreenBrightness` (earlier pass).
- Cycle includes **Off**; AlwaysShowMonitors keeps panel mesh with black screen + nameplate "Off".

## 1.0.14

- Cycle buttons are always a **bright orange cube** (unlit) — no longer clones tiny/invisible vanilla `CameraMonitorSwitchButton` meshes.
- Placement uses the panel face (two largest `localBounds` axes) with bottom-right + cabin-facing depth push.
- Larger default size; interact hitbox kept slightly larger than the visible mesh.

## 1.0.13

- Per-monitor **cycle button** (bottom-right): cycles that panel among living controlled players.
- **Click the monitor panel** to focus the ship's main radar / teleporter target.
- Sticky living assignments so refresh no longer overwrites a cycled player until they die/disconnect.

## 1.0.7

- Fix panels floating in the cabin: never clone Cube.001; prefer SingleScreen (Quad fallback).
- Parent clones to Cube.001 with localRotation=identity; place on the wall plane using Cube.001 MeshRenderer.localBounds.
- Does not change OpenBodyCams quality settings.

## 1.0.2

- Whitelist real screens only; PreferReuseVanillaScreens default false; clone grid relative to Cube.001.

## 1.0.1

- Host-gated networking where applicable, packaging polish.

## 1.0.0

- Initial release: per-player OpenBodyCams feeds on ship monitors.
