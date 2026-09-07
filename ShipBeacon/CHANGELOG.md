# Changelog

## 1.0.12
- Moved beacon to a fixed bottom position (default `VerticalOffset` 0.10) so it no longer sits in the clock area
- Center pivot / alignment for stable placement across moons

## 1.0.11
- Bottom pivot so the whole line sits above the anchor (no overlap into the clock)
- Default `VerticalOffset` 0.935
- Font size ~92% of clock size

# Changelog

## 1.0.12
- Moved beacon to a fixed bottom position (default `VerticalOffset` 0.10) so it no longer sits in the clock area
- Center pivot / alignment for stable placement across moons

## 1.0.10
- Orange matched to sampled clock HUD (~less yellow / more red)
- Ahead/behind caret arrows restored; side chevrons remain one-sided

## 1.0.9
- Fixed `ArgumentNullException` in `ApplyClockStyle` on cold start (TMP face/outline before material)

## 1.0.8
- Locked HUD orange (stopped copying clock faceColor/material that washed to white)
- Side-only `<<<` / `>>>` chevrons by bearing

## 1.0.7
- Larger text tracking clock `fontSize` (fallback 28)
- Brighter orange attempt + outline experiments

## 1.0.6
- Top placement styling; removed grey backdrop; clock-oriented defaults

## 1.0.5 and earlier
- TMP canvas HUD, HangarShip resolve, heartbeat hide-reason logs, Instance fix
