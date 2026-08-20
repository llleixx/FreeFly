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

The teleport menu releases the mouse cursor and blocks player movement while open. It lists current teammates, including passed-out and dead characters. Selecting one moves the local player to a point above and behind that character using PEAK's existing `WarpPlayerRPC`, so all clients see the same result and the game's collision/velocity cleanup remains in control.

## Build

The included `PeakGameDir.props` points at the standard Steam installation path. For another machine, edit it or copy `PeakGameDir.props.example`.

```powershell
dotnet build FreeFly.sln -c Release
dotnet msbuild src\FreeFly\FreeFly.csproj -t:Deploy -p:Configuration=Release
```

Normal builds only write `src/FreeFly/bin` and `obj`. `Deploy` copies `FreeFly.dll` to `PEAK/BepInEx/plugins/FreeFly`.

## Compatibility

The project is compiled against PEAK 2.2.a (Steam build 24801711) and BepInEx 5.4.75301. The Mod does not add Photon messages; teleport uses the game's existing RPC.
