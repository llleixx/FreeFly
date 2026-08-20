# FreeFly

FreeFly adds local free flight and a teammate teleport menu to PEAK.

## Controls

| Action | Keyboard | Controller |
|---|---|---|
| Toggle flight | `F6` | View / Select + Left Shoulder (default) |
| Open teleport menu | `F7` | View / Select + Right Shoulder (default) |
| Move | PEAK movement bindings | Left stick |
| Look | PEAK look bindings | Right stick |
| Ascend | Jump | A |
| Descend | Crouch | B |
| Temporary speed up | Hold Left Shift (default) | Hold Right Shoulder (default) |
| Temporary slow down | Hold Left Alt (default) | Hold Left Shoulder (default) |

F2, F3, and F4 are intentionally unused. All keyboard shortcuts, controller paths, and speed values can be changed in `BepInEx/config/com.github.lllei.FreeFly.cfg`.

`ControllerChordModifierPath`, `ControllerFlightTogglePath`, and `ControllerTeleportMenuTogglePath` configure the controller bindings as Unity Input System paths. The defaults are `<Gamepad>/selectButton`, `<Gamepad>/leftShoulder`, and `<Gamepad>/rightShoulder`, respectively. Leave the modifier path empty to use the two action buttons as single-button shortcuts; leave an action path empty to disable that shortcut.

`SpeedUpShortcut` and `SlowDownShortcut` configure the keyboard speed modifiers (default: Left Shift and Left Alt). `SpeedUpControllerButton` and `SlowDownControllerButton` configure their controller equivalents (default: Right Shoulder and Left Shoulder). Any of these can be set to `None` to disable that input.

The teleport menu releases the mouse cursor and blocks player movement while open. At the top it shows the two locations for the current stage: the stage start (the spawn point on stage 1, or the previous campfire afterwards) and the current stage's campfire. After a campfire advances the run, stage destinations remain disabled until PEAK has activated the new segment and marked its generation complete. In the Nadir it shows `Nadir start (Spawn)` and `Nadir waypoint (Scoutmaster Soul)`; after the soul is freed, the waypoint becomes the next-stage start and `Nadir end (The Gate)` points to PEAK's interactive `PeakGatePortal`. On the final PEAK segment, the destination is the highest `EndgameFlareSpawner` location, which is where PEAK places the summit signal flares; the game's final progress point and flare box are used as fallbacks. These entries are rebuilt when PEAK advances to the next stage, so locations from stages older than the current start are not left in the menu. It then lists current teammates, including passed-out and dead characters. Selecting a destination moves the local player to a point above and behind it using PEAK's existing `WarpPlayerRPC`, so all clients see the same result and the game's collision/velocity cleanup remains in control.

## Build

The included `PeakGameDir.props` points at the standard Steam installation path. For another machine, edit it or copy `PeakGameDir.props.example`.

```powershell
dotnet build FreeFly.sln -c Release
dotnet msbuild src\FreeFly\FreeFly.csproj -t:Deploy -p:Configuration=Release
```

Normal builds only write `src/FreeFly/bin` and `obj`. `Deploy` copies `FreeFly.dll` to `PEAK/BepInEx/plugins/FreeFly`.

## Compatibility

The project is compiled against PEAK 2.2.a (Steam build 24801711) and BepInEx 5.4.75301. The Mod does not add Photon messages; teleport uses the game's existing RPC.
