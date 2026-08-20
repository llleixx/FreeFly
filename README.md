# FreeFly

[![GitHub Repo](https://img.shields.io/badge/GitHub-llleixx%2FFreeFly-black?logo=github)](https://github.com/llleixx/FreeFly)
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/lllei/FreeFly?logo=thunderstore&label=Downloads)](https://thunderstore.io/c/peak/p/lllei/FreeFly/)

[English](#english) | [中文](#中文)

## English

FreeFly adds local no-clip flight to PEAK, with temporary speed control and a stage-aware teleport menu for finding teammates or reaching stage destinations.

### Features

- **Fly anywhere:** Toggle no-clip flight and move freely through the current stage without gravity or collision getting in the way.
- **Adjust speed on the fly:** Hold the configured speed-up or slow-down input for temporary multipliers, without changing your base speed.
- **Keyboard and controller support:** Use keyboard shortcuts or configurable Unity Input System controller paths. Flight and teleport can use a modifier chord or standalone buttons.
- **Stage-aware teleport destinations:** The menu tracks the current stage's start and end points, campfires, the Nadir route, and the final PEAK destination. Destinations are refreshed as the run advances.
- **Teleport to teammates:** Select living, passed-out, or dead teammates.

### Why FreeFly?

FreeFly is my own take on a flight mod, implemented around how I prefer to play. [FlyMode](https://thunderstore.io/c/peak/p/Luluberlu/FlyMode/) already exists, but I wanted a few things to work differently:

- **Speed should adapt to the situation:** The original mod does not provide temporary speed-up or slow-down controls. Moving faster is useful when crossing a large area, while a slower speed makes small adjustments much less frustrating.
- **Teleport belongs next to flight:** In practice, people usually enable a flight mod to find a teammate more quickly or get to the end of the current stage. Those destinations are useful enough that I wanted them in the same mod and menu.
- **Flight is also the recovery tool:** Teleporting is not always reliable and can occasionally leave the player stuck in an awkward position. Having flight available makes it possible to move back to a normal position instead of being left there.

There are also a couple of implementation details that are easy to miss during normal play:

- **Consistent no-clip:** The original mod approximates no-clip by moving the player very quickly while leaving the colliders active. At lower speeds, walls can still block you, and cacti or traps can still catch you or deal damage. FreeFly disables the character's flight colliders, so the result does not depend on speed and these hazards cannot affect you while flying.
- **Clean recovery:** FreeFly restores normal physics when flight ends, a warp starts, the character changes, or the plugin is unloaded.

### Installation

Install with Thunderstore Mod Manager or r2modman, or place `FreeFly.dll` directly in PEAK's `BepInEx/plugins` directory. FreeFly requires BepInEx 5.

### Controls

| Action | Keyboard | Controller |
|---|---|---|
| Toggle flight | `F6` | `View / Select` + `Left Shoulder` (default) |
| Open teleport menu | `F7` | `View / Select` + `Right Shoulder` (default) |
| Move | PEAK movement bindings | Left stick |
| Look | PEAK look bindings | Right stick |
| Ascend | PEAK jump binding | PEAK jump binding |
| Descend | PEAK crouch binding | PEAK crouch binding |
| Temporary speed up | Hold `Left Shift` (default) | Hold `Right Shoulder` (default) |
| Temporary slow down | Hold `Left Alt` (default) | Hold `Left Shoulder` (default) |

In the teleport menu, use Up/Down or the D-pad to select a destination, Enter/`A` to teleport, and Escape/`B` to cancel.

For normal stages, the menu shows the current stage start (the initial spawn or previous campfire) and its end campfire. Newly advanced destinations stay disabled while PEAK generates the segment. The Nadir exposes its spawn, Scoutmaster Soul waypoint, and The Gate as they become relevant; the final stage targets PEAK's summit flare location. Selecting any destination places the local player slightly above and behind it using configurable offsets.

### Configuration

The config file is generated at `BepInEx/config/com.github.lllei.FreeFly.cfg`.

| Key | Default | Allowed range |
|---|---:|---:|
| `General.Enabled` | `true` | boolean |
| `Controls.ToggleFlightShortcut` | `F6` | keyboard key |
| `Controls.TeleportMenuShortcut` | `F7` | keyboard key |
| `Controls.ControllerChordModifierPath` | `<Gamepad>/selectButton` | Unity Input System path, or empty |
| `Controls.ControllerFlightTogglePath` | `<Gamepad>/leftShoulder` | Unity Input System path, or empty |
| `Controls.ControllerTeleportMenuTogglePath` | `<Gamepad>/rightShoulder` | Unity Input System path, or empty |
| `Controls.SpeedUpShortcut` | `LeftShift` | keyboard key, or `None` |
| `Controls.SlowDownShortcut` | `LeftAlt` | keyboard key, or `None` |
| `Controls.SpeedUpControllerPath` | `<Gamepad>/rightShoulder` | Unity Input System path, or empty/`None` |
| `Controls.SlowDownControllerPath` | `<Gamepad>/leftShoulder` | Unity Input System path, or empty/`None` |
| `Movement.BaseSpeed` | `100` m/s | `1` to `1000` |
| `Movement.SpeedUpMultiplier` | `2.0` | `1` to `10` |
| `Movement.SlowDownMultiplier` | `0.2` | `0.05` to `1` |
| `Teleport.VerticalOffset` | `2` m | `0` to `10` |
| `Teleport.BackwardOffset` | `1.5` m | `0` to `10` |

Controller paths are Unity Input System paths. Leave the modifier path empty to make the flight and teleport actions standalone buttons; leave an action path empty to disable that shortcut. Invalid numeric values are clamped or replaced with safe defaults at runtime.

### Compatibility notes

FreeFly is compiled against PEAK 2.2.a (Steam build 24801711) and BepInEx 5.4.75301. Only the player using FreeFly needs to install it.

### Building

The included `PeakGameDir.props` points at the standard Steam installation path. For another machine, copy `PeakGameDir.props.example` to `PeakGameDir.props` and set the PEAK directory, or set `PEAK_GAME_DIR`.

```powershell
dotnet test tests\FreeFly.Core.Tests\FreeFly.Core.Tests.csproj -c Release
dotnet build FreeFly.sln -c Release
dotnet msbuild src\FreeFly\FreeFly.csproj -t:Deploy -p:Configuration=Release
dotnet msbuild src\FreeFly\FreeFly.csproj -t:PackageThunderstore -p:Configuration=Release
```

Normal builds only write `src/FreeFly/bin` and `obj`. `Deploy` copies `FreeFly.dll` to `PEAK/BepInEx/plugins/FreeFly`; `PackageThunderstore` creates `artifacts/lllei-FreeFly-<version>.zip` after checking the manifest version.

## 中文

FreeFly 为 PEAK 增加本地无碰撞自由飞行、临时调速和按关卡更新的传送菜单，可用于寻找队友或前往关卡目的地。

### 功能亮点

- **自由穿行：** 开启无碰撞飞行后，可以在当前关卡中自由移动，不受重力和碰撞影响。
- **沿用 PEAK 操作：** 水平移动和视角使用游戏当前绑定，跳跃与蹲下分别控制上升和下降。
- **临时变速：** 按住可配置的加速或减速输入即可临时调整速度，不会改变基础速度。
- **键盘与手柄均可用：** 支持键盘快捷键和可配置的 Unity Input System 手柄路径；飞行和传送可以使用组合键，也可以改为单键。
- **按关卡管理目的地：** 菜单会显示当前关卡起点、篝火、终点，以及 Nadir 路线和 PEAK 最终目的地；推进关卡后会自动刷新。
- **传送到队友：** 可选择存活、昏迷或死亡的队友；死亡队友使用其尸体/观战位置。
- **兼容多人游戏：** 传送结果会同步给房间内所有玩家，队友无需安装 FreeFly。

### 为什么做 FreeFly

FreeFly 是我按照自己的使用习惯实现的飞行模组。[FlyMode](https://thunderstore.io/c/peak/p/Luluberlu/FlyMode/) 已经提供了飞行功能，但我希望下面几件事能更符合实际使用场景：

- **速度应该适应不同场景：** 原 Mod 没有临时加速或减速。跨越大范围区域时需要更快的速度，而接近目标、微调位置时又需要更慢的速度。
- **传送适合和飞行放在一起：** 实际上，使用飞行模组通常就是为了更快找到队友，或者直接前往当前关卡终点。因此我把这些常用目的地集成到了同一个 Mod 和菜单中。
- **飞行也应该能用来善后：** 传送并不总是稳定，偶尔会把玩家卡在不合适的位置。有了飞行，就可以自己回到正常位置，而不是只能被困在那里。

还有一些在正常使用中不一定马上察觉到的实现差异：

- **稳定的无碰撞效果：** 原 Mod 保留碰撞体，主要通过高速移动来近似实现无碰撞。速度较低时仍可能被墙挡住，而且无论速度如何，仙人掌和陷阱仍可能卡住玩家或造成伤害。FreeFly 在飞行时会关闭角色碰撞体，因此效果不依赖移动速度，飞行过程中也不会受到这些障碍和陷阱影响。
- **完整恢复状态：** 结束飞行、开始传送、更换角色或卸载 Mod 时，FreeFly 会恢复正常物理状态。

### 安装

可以通过 Thunderstore Mod Manager 或 r2modman 安装，也可以将 `FreeFly.dll` 直接放入 PEAK 的 `BepInEx/plugins` 目录。FreeFly 需要 BepInEx 5。

### 操作方法

| 操作 | 键盘 | 手柄 |
|---|---|---|
| 切换飞行 | `F6` | `View / Select` + `左肩键`（默认） |
| 打开传送菜单 | `F7` | `View / Select` + `右肩键`（默认） |
| 移动 | PEAK 移动绑定 | 左摇杆 |
| 观察 | PEAK 视角绑定 | 右摇杆 |
| 上升 | PEAK 跳跃绑定 | PEAK 跳跃绑定 |
| 下降 | PEAK 蹲下绑定 | PEAK 蹲下绑定 |
| 临时加速 | 按住 `Left Shift`（默认） | 按住 `右肩键`（默认） |
| 临时减速 | 按住 `Left Alt`（默认） | 按住 `左肩键`（默认） |

在传送菜单中，使用方向键/十字键选择目的地，回车键/`A` 确认传送，Escape/`B` 取消。

普通关卡会显示当前关卡起点（初始出生点或上一处篝火）和终点篝火；推进关卡后，新目的地会在 PEAK 完成地图生成前保持禁用。Nadir 会按流程显示出生点、Scoutmaster Soul 路标和 The Gate，最终关卡则指向 PEAK 山顶的信号弹位置。传送时会根据可配置偏移，将本地玩家放到目的地的上方和后方。

### 配置

配置文件生成于 `BepInEx/config/com.github.lllei.FreeFly.cfg`。配置项、默认值和范围见英文表格。手柄路径使用 Unity Input System 格式：将组合键修饰键路径留空可改为单键，将动作路径留空可禁用对应快捷键。非法数值会在运行时钳制或回退到安全值。

### 兼容性说明

FreeFly 编译于 PEAK 2.2.a（Steam build 24801711）和 BepInEx 5.4.75301。只有使用 FreeFly 的玩家需要安装，队友无需安装。

### 构建

项目内的 `PeakGameDir.props` 默认指向 Steam 安装目录。其他机器请将 `PeakGameDir.props.example` 复制为 `PeakGameDir.props` 并填写 PEAK 路径，或设置 `PEAK_GAME_DIR`。构建、测试、部署和 Thunderstore 打包命令见英文构建章节。
