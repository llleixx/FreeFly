# Changelog

All notable changes to FreeFly are documented here.

## 1.1.1 - 2026-08-23

- Fixed descending with the controller crouch button during free flight.

## 1.1.0 - 2026-08-22

- Added on-screen notifications when free flight is enabled or disabled.
- Refactored input, flight runtime, teleport destinations, and the teleport menu into separate modules.
- Improved flight cleanup by restoring the original gravity and collider states.
- Reduced teleport menu refresh and GUI allocation overhead while keeping destinations updated during generation.
- Added validation and complete archive contents to the Thunderstore packaging script.

## 1.0.0

- Added local no-clip flight with PEAK's native movement, look, jump, and crouch inputs.
- Added temporary speed-up and slow-down modifiers without changing the base flight speed.
- Added configurable keyboard shortcuts and Unity Input System controller paths.
- Added a stage-aware teleport menu with campfires, Nadir destinations, the PEAK summit, and teammate positions.
- Added teleport targets for alive, passed-out, and dead teammates.
- Added cleanup when flight becomes unavailable, the character changes, PEAK starts a warp, or the plugin unloads.
