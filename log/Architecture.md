# 项目结构与用法报告

> 这份是给"审计"用的：把**文件、数据流、依赖方向**摊开，最后单列一节
> **「耦合与偏离自查」**——那些做完之后我自己觉得可疑、需要你拍板的地方。
> 日常怎么用（加球、加组件、约定与坑）在 `FunctionGuide.md`，这里不重复。
>
> 结论先放这儿：**功能是通的（实机跑过），但有 3 处结构性耦合值得处理**——
> ①装配器认识每一个组件；②"外观/尺寸"有四个入口；③事件注册有两种写法。

## 一、现在是什么

| 项 | 数 |
| --- | --- |
| C# 文件 | 36 个，约 3300 行 |
| 场景 | 6 个（`index` / `game` / `NormalBall` / `BallPicker` / `BallButton` / `BallDataPanel`） |
| 球 | 2 颗（`NormalBall` 贴图球、`pulipuli` Spine 球） |
| 组件 | 9 个（1001 / 2001 / 2002 / 2003 / 4001 / 4002 / 5001 / 5002 / 6001） |
| 外部依赖 | Godot 4.7.1（带 C# 的自编译版）+ Spine GDExtension（`addons/spine-godot`） |

玩法一句话：选球 → 3 秒倒计时 → 两颗球按组件打（碰撞、下蛋、减速、中毒）→ 只剩一个阵营时结算。

## 二、目录结构（文件级）

| 路径 | 放什么 | 谁在用 |
| --- | --- | --- |
| `assets/scripts/basic/Ball.cs` | 小球本体：血量、五个状态、受控/攻击的计时、事件总线入口 | 所有组件、装配器、面板 |
| `assets/scripts/basic/BallEvent.cs` | 轻量事件总线：按优先级排、返回 false 阻塞 | `Ball.Events`，各组件注册 |
| `assets/scripts/basic/BallComponent.cs` | 组件基类（自描述：编号/类型名/说明/前置） | 所有组件 |
| `assets/scripts/basic/BallMovement.cs` | "推进 + 撞墙反弹"这一小段共用逻辑 | `NormalMove`（移动态）、`Slow`（受控态） |
| `assets/scripts/basic/BallState.cs` | 五个状态枚举（登场/移动/受控/攻击/死亡） | `Ball`、各组件的状态门控 |
| `assets/scripts/basic/DamagePriority.cs` | 受伤链优先级表（1000 无敌 … 500 一般扣血） | 装配器注册、组件参考 |
| `assets/scripts/basic/EventName.cs` | 事件名常量（`take_damage` / `state_changed`） | 所有注册事件的地方 |
| `assets/scripts/basic/BallAssembler.cs` | **装配中心**：读数据 → 建球 → 换外观 → 挂组件 → 接线 | `Game._Ready`、`EggAttack.LayEgg` |
| `assets/scripts/basic/Game.cs` | 战斗场景：读双方球 → 装配 → 倒计时 → 放球 → 结算 → 暂停 | 场景 `game.tscn` |
| `assets/scripts/basic/GameManager.cs` | 局单例（autoload）：记住双方选球、切场景、回主菜单 | 选球页、结算提示 |
| `assets/scripts/components/` | 具体组件（见 `FunctionGuide.md` 的组件总表） | 由装配器按 Json 编号创建 |
| `assets/scripts/components/ComponentLibrary.cs` | 编号 → 组件实例的翻译表 | `BallAssembler` |
| `assets/scripts/tool/BallData.cs` | `balldata.json` 的内存模型 | 装配器、`Game` |
| `assets/scripts/tool/BallLibrary.cs` | 扫 `user://balls` 列出所有球（选球界面用）、读头像 | `SelectPage`、`Shift`（读 user:// 图片） |
| `assets/scripts/tool/DataSeeder.cs` | 首次/每次启动把仓库默认数据补到 `user://`（**只补缺不覆盖**） | `Bootstrap` |
| `assets/scripts/tool/UserData.cs` | `user://` 目录布局常量 | 几乎所有碰文件的地方 |
| `assets/scripts/tool/JsonTool.cs` | 通用 Json 读工具（带缓存、点号取子键） | 所有读 Json 的地方 |
| `assets/scripts/tool/SoundTool.cs` | 从 `user://` 读音频、缓存、播一遍 | 目前只有 `2001` 用 |
| `assets/scripts/tool/Bootstrap.cs` | autoload：启动时补数据 | 引擎启动 |
| `assets/scripts/ui/` | 界面：页面管理、选球、数据面板、血条、结算提示 | `index.tscn` / `game.tscn` |
| `assets/scene/*.tscn` | 场景与预制体（`NormalBall.tscn` 是所有球共用的预制体） | 引擎 |
| `assets/data/balls/<球id>/` | 球的**默认数据**（`balldata.json` + `resource/`） | 被补数据复制到 `user://` |
| `assets/data/scene/arena.json` | 场地参数 | **目前没人读** |
| `assets/theme/ui_theme.tres` | 中文字体主题 | 所有界面 |
| `resources/` | 你放素材的**暂存区**（`avatar.png`、`shit.png`） | `Shift` 默认素材指向这里；注意它在 `res://` 里，会被引擎导入 |
| `addons/spine-godot/` | Spine GDExtension（全平台二进制） | `SpineLook` 动态调用 |
| `log/` | 文档（本文件、`FunctionGuide.md`、`README.md`） | 人 |

## 三、一局的完整生命周期

1. **启动**：引擎跑 autoload → `Bootstrap._Ready` → `DataSeeder.SeedMissing()` 把 `res://assets/data/` 的缺失文件补进 `user://`（只补缺，不覆盖）。
2. **主界面**：`index.tscn` → `IndexManager` 把直接子节点当页面（`Index` / `Select`）。
3. **选球**：`SelectPage._Ready` → `BallLibrary.LoadAll()` 扫 `user://balls/` → 左右两个 `BallPicker` 各铺一排 `BallButton`；双方确认后才放开"开始游戏"。
4. **开局**：`GameManager.StartGame(id1, id2)` 记下选择 → 切 `game.tscn`。
5. **装配**（`Game._Ready`）：`BallData.Load` 读配置 → `BallAssembler.Build` ×2：建球 → 按 `type` 换外观（贴图 / Spine）→ 按编号挂自己的组件 → `Wire` 接线 → `AddChild` → 互相把 `enemycomponents` 挂到对面身上。
6. **倒计时 3 秒**：两颗球都在**登场**状态（`NormalMove` 不动手，Spine 播 idle）。
7. **开打**：`FinishSpawn()` → 进**移动**状态 → `NormalMove` 开始驱动；球自己的 `_PhysicsProcess` 管的只有受控/攻击两个计时。
8. **打起来**：`HitArea`（`Area2D`）检测到对方 → `TakeDamage(值)` → `BallEvent` 按优先级跑受伤链（无敌 1000 → 沉默 900 → 真伤 800 → 护盾 700 → 中毒染色 600 → 一般扣血 500，任何一环返回 false 就截断）。
9. **结算**：`Game._Process` 每帧看 `balls` 组里还有几个阵营活着 → 只剩一个就显示结果、`GetTree().Paused = true`、亮"按任意处退出"。
10. **回主菜单**：`ResultPrompt` 收到输入 → `GameManager.ReturnToMenu()`（先解暂停再切场景）。

## 四、数据流（三份数据，别搞混）

| 位置 | 是什么 | 谁写 |
| --- | --- | --- |
| `res://assets/data/balls/<球id>/` | **仓库里的默认值**，入库、给新玩家用 | 你/我手写 |
| `user://balls/<球id>/` | **运行时真正读的**（Windows 在 `%APPDATA%\Liya\app_userdata\ShineBallGame\balls\`） | 补数据复制 + 你直接改 |
| 内存（`BallData` / `BallEntry`） | 装配时读进来的那份 | 读一次，不回头 |

两个必记的后果：

* **改仓库里的 `balldata.json` 对已经跑过的机器无效**——要生效就改 `user://` 那份，或者把它删掉让补数据重来（我这几轮都是这么验的）。
* **只补缺、不覆盖**：新加的文件（比如 Spine 的三件套、音效）会自动补过去；已存在的文件永远不动。

## 五、依赖方向

```
ui  ──→  tool, basic            （界面只用工具和局管理器）
Game ──→  tool(BallData), basic(BallAssembler)
BallAssembler ──→ components(ComponentLibrary + 每个组件的类型)
              └─→ tool(UserData, BallLibrary, SoundTool)
components ──→ basic(Ball, BallMovement, BallEvent, EventName, DamagePriority)
            └─→ tool(JsonTool, BallLibrary, UserData, SoundTool)
basic(Ball) ──→ 只认识 BallEvent/BallState，不认识任何具体组件
```

**唯一一条"往上"的依赖是装配器**：它认识每一个组件类型（`Switch` 分支），组件们反过来都不认识它。
好处是"谁需要接线"一目了然；代价是**每加一个组件都要动装配器**——这是全项目最集中的耦合点。

## 六、现状清单

| 系统 | 状态 | 备注 |
| --- | --- | --- |
| 选球 / 开局 / 倒计时 / 结算 / 暂停 / 回主菜单 | ✅ 实机通过 | |
| 五状态状态机 | ✅ 都在用 | 受控、攻击的计时在 `Ball` 里 |
| 事件总线（优先级 + 阻塞） | ✅ 实机通过 | |
| 组件 1001 / 2001 / 2002 / 2003 / 4001 / 4002 / 5001 / 5002 / 6001 | ✅ 都跑过 | 见组件总表 |
| 外观：`type=1` 贴图 / `type=2` Spine | ✅ 实机通过 | Spine 按状态切动画，缺动画走兜底链 |
| 音效 | ⚠️ 只接了 `2001` | `SoundTool` 通用，别的组件想用得自己调 |
| 控制（减速） | ✅ 实机通过 | 减速 = 受控状态下由 6001 接手驱动（30%） |
| 防御类 | ⚠️ 只有 `3001` 条件无敌 | 护盾 / 真伤 / 沉默还只有优先级位 |
| `arena.json`（场地参数） | ❌ 没人读 | `Game.cs` 里出生点是写死的 |
| `BallState.Attack` 的"动作" | ⚠️ 只有下蛋用 | 攻击状态本身已经可用 |
| 蛋（`2003`） | ✅ 实机通过 | 普通节点（不是球）：不进 `balls` 组、不参与胜负；参数只能改组件默认值 |
| 版本管理 | ⚠️ 一大批没提交 | 见下面审计项 14 |

## 七、耦合与偏离自查（明天重点看这节）

按"我建议的处理优先级"排。前三条是结构性的，后面是具体坑。

### 1. 装配器认识每一个组件（最集中）

* **位置**：`BallAssembler.Wire` 的 switch、`SetAppearance`、`SetSpineAppearance`、`BuildEgg`。
* **现象**：加一个需要接线的组件，就要在装配器里加一个 `case`；加一种外观，就要加一个分支。
* **为什么可疑**：这是唯一"核心认识所有细节"的地方，组件越多它越长。
* **注意**：**这是你定的约定**（"接线在 `BallAssembler.Wire`"），不是偏离。要不要改由你定。
* **可选方向**：给 `BallComponent` 加一个 `Bind(Ball)` 虚方法，组件自己接线；装配器只做"建 → 挂 → 调 Bind"。约定要改，但装配器会瘦成 20 行。

### 2. "外观 / 尺寸"有四个入口

* **位置**：`BallAssembler.SetBodyTexture`（type1 贴图）、`SpineLook`（type2）、`Shift.BuildLook`（蛋的图 + 检测圈）、`CircleShape`（碰撞圈）。
* **现象**：想改"球长什么样"的规则，得翻四个地方；碰撞圈的逻辑还写了两遍（5001 与 `SetCircle`）。
* **为什么可疑**：这是最可能"改一处忘一处"的地方。
* **建议**：抽一层"外观"（`Look`），三个来源（贴图 / Spine / 蛋）都走它，碰撞圈只留一个入口。

### 3. 事件注册有两种写法

* **位置**：`NormalMove` / `CollisionAttack` / `EggAttack` / `Slow` / `SpineLook` 在**自己的 `_Ready`** 里注册；而 `NormalDamage` / `Poison` 由**装配器**注册。
* **为什么可疑**：同一个概念两种规矩，看代码的人要先猜"这个组件的注册在哪"。纯属历史原因（早期只有 4001，装配器顺手注册了）。
* **建议**：统一到组件自己注册（`_Ready` 或 `Bind`），装配器不碰事件。

### 4. `Ball` 里混着玩法的时长

* **位置**：`Ball._controlLeft` / `_attackLeft` + `TickControlled` / `TickAttack`。
* **现象**：状态机知道"受控几秒、攻击几秒"。
* **为什么可疑**：这是**刻意的**耦合（状态要能自己走出去），但意味着以后"某个状态时长由组件定"要改 `Ball`。
* **建议**：知道它在就行，暂时不用动。

### 5. `Ball.Id` 和 `ball.Name` 是两份

* **位置**：`Ball.Id`（后加）与 `ball.Name = data.Id`。
* **现象**：两颗同种球相撞时引擎会改 `Name`（日志里出现过 `@CharacterBody2D@41`），`Id` 不受影响。
* **建议**：以后统一用 `Id`；`Name` 只当调试标签。可以再顺手给 `Name` 加序号，日志好认。

### 6. 伤害载荷是裸 `float`

* **位置**：`Ball.TakeDamage(float)` → `Events.Trigger(take_damage, damage)`。
* **现象**：没有伤害来源、也没有"剩余伤害"的概念。
* **后果**：护盾（700，非阻塞）想"扣掉一部分、剩下的继续往后传"**做不到**；"只有毒伤才触发中毒"也做不到。
* **建议**：尽早换成 `DamageEvent { Amount, Source, ... }`。**这是挡着整个 3xxx 防御类的那块石头**。

### 7. 控制类组件不能共存

* **位置**：`Slow._PhysicsProcess` 直接驱动球。
* **现象**：一颗球挂两个控制类组件，两个都会驱动一次 → 球跑得比预期快。
* **建议**：球那边做个"谁驱动"的登记，按优先级选一个（这也是 `priority` 真正的用武之地）。

### 8. Spine 那一层没有编译期检查

* **位置**：`SpineLook` 全程 `ClassDB.Instantiate` + `Call("方法名")`。
* **原因**：C# 里根本没有 Spine 的类（GDExtension 只对引擎/GDScript 可见），无解——除非把这一层用 GDScript 写。
* **风险**：方法名、属性名写错只在运行时炸；Spine 扩展升级也可能改动 API。
* **建议**：保持这一层薄（现在已经够薄），改的时候一定要跑一次实机。

### 9. 蛋没有自己的数据

* **位置**：`Shift` 把外观（贴图/尺寸）和行为参数写在自己的默认值里；`EggAttack.egg_id` 只能指定一个组件。
* **后果**：想换一种蛋 = 新写一个组件；蛋的行为参数只能改组件的默认值。
* **建议**：等你要做第二、第三种蛋时，扩成"蛋也读一份数据"。

### 10. 音效的归属

* **位置**：`SoundTool.Resolve(sound, ballId)` 按"被挂的那颗球"的目录找文件。
* **现象**：通过 `enemycomponents` 挂给对面的攻击组件，音效会去**对面**的目录找。
* **现状**：现在没人这么用，碰不到；要用之前得先把"配置来自哪颗球"传进去。

### 11. 场景里的数字 vs 数据里的数字

* **位置**：`Game.cs` 出生点写死 700/1220、墙面写在 `game.tscn`；而 `arena.json` 里是 300/1620。
* **建议**：要么删掉 `arena.json`，要么把它接上（别让它一直是一份"看起来有用其实没读"的数据）。

### 12. `resources/` 在 `res://` 里

* **现象**：你的素材暂存区被引擎扫描并导入（生成了 `.import` 文件）；球正式用的素材应该在 `assets/data/balls/<球id>/resource/`。
* **建议**：`resources/` 只当草稿区，或者干脆挪到工程外。

### 13. 未提交的改动

本次会话新增/改动的东西都还在工作区（`git status` 有 12 个改过的文件 + 一堆未跟踪的新文件：`addons/`、`pulipuli/`、`CircleShape.cs` / `EggAttack.cs` / `Shift.cs` / `Slow.cs` / `Poison.cs` / `SpineLook.cs` / `SoundTool.cs` / `BallMovement.cs` 等）。
**建议明天审计前先提交一次**，这样你改的时候有基线可比。

## 八、快速用法（细节看 `FunctionGuide.md`）

| 想干什么 | 怎么做 |
| --- | --- |
| 加一颗球 | 建 `assets/data/balls/<id>/` → 写 `balldata.json` → 放 `resource/avatar.png`（Spine 球再放 atlas+骨架）→ 跑一次让补数据复制 |
| 加一个组件 | `components/` 下写类（**文件名 = 类名**）→ `ComponentLibrary` 加一行 → 需要接线就在 `BallAssembler.Wire` 加分支 → 编号按 1xxx 移动 / 2xxx 攻击 / 3xxx 防御 / 4xxx 行为 / 5xxx 形态 / 6xxx 控制 |
| 编译 | `NUGET_PACKAGES=C:\Users\24807\.nuget\packages` 后 `dotnet build ShineBallGame.csproj`（编辑器的构建按钮不可用） |
| 跑一局看日志 | `godot.windows.editor.x86_64.mono.console.exe --headless --path D:\shine-ball-game res://assets/scene/index.tscn --fixed-fps 60 --quit-after N`（`--fixed-fps` 必须加，否则倒计时/周期逻辑推不动） |
| 直接打一场 | 临时脚本里 `GameManager.StartGame("球A", "球B")` 再跑帧（我这几次的验证都是这么做的） |
| 看 `user://` 在哪 | `%APPDATA%\Liya\app_userdata\ShineBallGame\` |

## 九、我建议的下一步顺序

1. **伤害载荷换成对象**（`DamageEvent`）——它挡着 3xxx 一整套和"按来源区分"。
2. **外观层收口**——四套入口变一套，后面加球/加蛋都省事。
3. **事件注册统一**——消灭"有的在组件、有的在装配器"。
4. **控制组件抢方向盘**——为"又减速又眩晕"做准备。
5. **`arena.json` 接线或删掉**——别留"看起来有用"的死数据。
