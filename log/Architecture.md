# 项目结构与用法报告

> 这份是给"审计"用的：把**文件、数据流、依赖方向**摊开，最后单列一节
> **「耦合与偏离自查」**——那些做完之后我自己觉得可疑、需要你拍板的地方。
> 日常怎么用（加球、加组件、约定与坑）在 `FunctionGuide.md`，这里不重复。
>
> 结论先放这儿：**功能是通的（实机跑过）**。上一版点名的 3 处结构性耦合
> （装配器认识每个组件 / 外观有四个入口 / 事件注册两种写法）**已经处理掉了**，
> 见下方自查第 1、2、3 条——现在"加组件"不用动装配器、"加外观"只有一处入口、
> 事件一律组件自己注册。剩下的都是具体坑。

## 一、现在是什么

| 项 | 数 |
| --- | --- |
| C# 文件 | 42 个，约 3740 行 |
| 场景 | 6 个（`index` / `game` / `NormalBall` / `BallPicker` / `BallButton` / `BallDataPanel`） |
| 球 | 2 颗（`NormalBall` 贴图球、`pulipuli` Spine 球） |
| 组件 | 9 个（1001 移动 / 2001 碰撞 / 2002 下蛋 / 3001 条件无敌 / 4001 受伤 / 4002 中毒染色 / 4003 进状态音效 / 5001 碰撞箱 / 6001 减速） |
| 蛋 | 1 种（`2003` 屎蛋，是 `Egg` 的子类，不是组件） |
| 外部依赖 | Godot 4.7.1（带 C# 的自编译版）+ Spine GDExtension（`addons/spine-godot`） |

玩法一句话：选球 → 3 秒倒计时 → 两颗球按组件打（碰撞、下蛋、减速、中毒）→ 只剩一个阵营时结算。

## 二、目录结构（文件级）

| 路径 | 放什么 | 谁在用 |
| --- | --- | --- |
| `assets/scripts/basic/Ball.cs` | 小球本体：血量、五个状态、受控/攻击的计时、事件总线入口 | 所有组件、装配器、面板 |
| `assets/scripts/basic/BallEvent.cs` | 轻量事件总线：按优先级排、返回 false 阻塞 | `Ball.Events`，各组件注册 |
| `assets/scripts/basic/BallComponent.cs` | 组件基类（自描述 + `Bind` 接线钩子） | 所有组件 |
| `assets/scripts/basic/BallLook.cs` | **外观层**：贴图 / Spine / 碰撞圈 / 血条，唯一入口 | 装配器、`5001`、蛋 |
| `assets/scripts/basic/SpineLook.cs` | Spine 那一层（很薄，全程 `Call` 字符串调用） | `BallLook` |
| `assets/scripts/basic/Egg.cs` | 蛋的基类（`Area2D` + 图 + 检测圈 + 消失），一种蛋一个子类 | `EggLibrary` |
| `assets/scripts/basic/EggLibrary.cs` | 蛋的编号表（编号 → 蛋类） | `BallAssembler.BuildEgg` |
| `assets/scripts/eggs/ShitEgg.cs` | 屎蛋：撞到人 → 减速 + 每秒跳伤 → 自己消失 | `EggLibrary` |
| `assets/scripts/basic/DamageEvent.cs` | 伤害事件：还剩多少(`Amount`) / 原始值(`Original`) / 谁打的(`Source`/`SourceId`) / 哪种攻击(`SourceType`) | `Ball.TakeDamage`、受伤链上的每个组件 |
| `assets/scripts/basic/BallMovement.cs` | "推进 + 撞墙反弹"这一小段共用逻辑 | `NormalMove`（移动态）、`Slow`（受控态） |
| `assets/scripts/basic/BallState.cs` | 五个状态枚举（登场/移动/受控/攻击/死亡） | `Ball`、各组件的状态门控 |
| `assets/scripts/basic/DamagePriority.cs` | 受伤链优先级表（1000 无敌 … 500 一般扣血） | 装配器注册、组件参考 |
| `assets/scripts/basic/EventName.cs` | 事件名常量（`take_damage` / `state_changed`） | 所有注册事件的地方 |
| `assets/scripts/basic/BallAssembler.cs` | **装配中心**：建球 → 摆外观 → 挂组件（调 `Bind`）→ 摆血条 / 产蛋。**不认识任何具体组件** | `Game._Ready`、`EggAttack.LayEgg` |
| `assets/scripts/basic/Game.cs` | 战斗场景：读双方球 → 装配 → 倒计时 → 放球 → 结算 → 暂停 | 场景 `game.tscn` |
| `assets/scripts/basic/GameManager.cs` | 局单例（autoload）：记住双方选球、切场景、回主菜单 | 选球页、结算提示 |
| `assets/scripts/components/` | 具体组件（见 `FunctionGuide.md` 的组件总表） | 由装配器按 Json 编号创建 |
| `assets/scripts/components/ComponentLibrary.cs` | 编号 → 组件实例的翻译表 | `BallAssembler` |
| `assets/scripts/tool/BallData.cs` | `balldata.json` 的内存模型 | 装配器、`Game` |
| `assets/scripts/tool/BallLibrary.cs` | 扫 `user://balls` 列出所有球、读图；`Find` 是**小球独特资源的统一找法** | `SelectPage`、`Egg`、`SoundTool`、`BallLook` |
| `assets/scripts/tool/DataSeeder.cs` | 首次/每次启动把仓库默认数据补到 `user://`（**只补缺不覆盖**） | `Bootstrap` |
| `assets/scripts/tool/UserData.cs` | `user://` 目录布局常量 | 几乎所有碰文件的地方 |
| `assets/scripts/tool/JsonTool.cs` | 通用 Json 读工具（带缓存、点号取子键） | 所有读 Json 的地方 |
| `assets/scripts/tool/SoundTool.cs` | 读音频（走 `BallLibrary.Find`）、缓存、播一遍 | `2001` 撞击音、`4003` 进状态音 |
| `assets/scripts/tool/Bootstrap.cs` | autoload：启动时补数据 | 引擎启动 |
| `assets/scripts/ui/` | 界面：页面管理、选球、数据面板、血条、结算提示 | `index.tscn` / `game.tscn` |
| `assets/scene/*.tscn` | 场景与预制体（`NormalBall.tscn` 是所有球共用的预制体） | 引擎 |
| `assets/data/balls/<球id>/` | 球的**默认数据**（`balldata.json` + `resource/`） | 被补数据复制到 `user://` |
| `assets/data/scene/arena.json` | 场地参数 | **目前没人读** |
| `assets/theme/ui_theme.tres` | 中文字体主题 | 所有界面 |
| `resources/` | 你放素材的**暂存区**（`avatar.png`、`shit.png`） | `Shift` 默认素材指向这里；注意它在 `res://` 里，会被引擎导入 |
| `addons/spine-godot/` | Spine GDExtension（全平台二进制，**53MB，没入库**） | `SpineLook` 动态调用；只留 windows 版还是全提交，待定 |
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
components ──→ basic(Ball, BallMovement, BallEvent, EventName, DamagePriority, DamageEvent)
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
| 组件（11 个，见组件总表） | ✅ 都跑过 | 移动 / 碰撞 / 下蛋 / 屎蛋 / 条件无敌 / 受伤 / 中毒染色 / 进状态音效 / 碰撞箱 / Spine 外观 / 减速 |
| 外观：`type=1` 贴图 / `type=2` Spine | ✅ 实机通过 | Spine 按状态切动画，缺动画走兜底链 |
| 音效 | ⚠️ 只接了 `2001` | `SoundTool` 通用，别的组件想用得自己调 |
| 控制（减速） | ✅ 实机通过 | 减速 = 受控状态下由 6001 接手驱动（30%） |
| 防御类 | ⚠️ 只有 `3001` 条件无敌 | 护盾 / 真伤 / 沉默还只有优先级位 |
| 伤害载荷 | ✅ `DamageEvent` | 带来源、可被中途改写（`Amount`）；扣血统一在 `4001`，并打 `[伤害]` 日志 |
| `arena.json`（场地参数） | ❌ 没人读 | `Game.cs` 里出生点是写死的 |
| `BallState.Attack` 的"动作" | ⚠️ 只有下蛋用 | 攻击状态本身已经可用 |
| 蛋（`2003`） | ✅ 实机通过 | 普通节点（不是球）：不进 `balls` 组、不参与胜负；参数只能改组件默认值 |
| 版本管理 | 🟡 随手提交 | 每批改完就 `feat:` / `fix:` 提交，别攒着（见自查第 13 条） |

## 七、耦合与偏离自查（明天重点看这节）

按"我建议的处理优先级"排。前三条是结构性的，后面是具体坑。

### 1. 装配器认识每一个组件（✅ 已处理）

* **当时的做法**：`BallAssembler.Wire` 里一个 `case` 接一个组件，加组件必改装配器。
* **现在**：`BallComponent` 多了 `Bind(Ball ball, string configId)` 钩子——**组件自己接线**
  （注册事件、连信号、改形状），装配器只做"建 → 填参数 → 挂 → 调 `Bind`"，**不认识任何具体组件**。
  `configId` 顺着传下去，顺带解决了"从 `enemycomponents` 送出去的组件该用谁的包找资源"。
* **代价**：`Bind` 必须在**装配期**（球进树之前）跑，而且要比摆血条早——已写进 `FunctionGuide` 的约定清单。

### 2. "外观 / 尺寸"有四个入口（✅ 已处理）

* **当时的做法**：贴图在装配器、Spine 在 `SpineLook`、蛋在自己身上、碰撞圈在 `5001` 里各写一份。
* **现在**：`basic/BallLook.cs` 是**唯一入口**——贴图 / Spine / 显示尺寸 / 碰撞圈 / 血条位置全在这里；
  `5001` 只调 `BallLook.SetCircle`，蛋只调 `BallLook.FitSprite`。想改"球多大 / 判定多大"只改这一个文件。

### 3. 事件注册有两种写法（✅ 已处理）

* **当时的做法**：一半组件在自己 `_Ready` 里注册，`4001`/`4002` 由装配器注册。
* **现在**：**一律在组件自己的 `Bind` 里注册**，装配器完全不碰事件。看任何一个组件，接线都在同一个地方。

### 4. `Ball` 里混着玩法的时长

* **位置**：`Ball._controlLeft` / `_attackLeft` + `TickControlled` / `TickAttack`。
* **现象**：状态机知道"受控几秒、攻击几秒"。
* **为什么可疑**：这是**刻意的**耦合（状态要能自己走出去），但意味着以后"某个状态时长由组件定"要改 `Ball`。
* **建议**：知道它在就行，暂时不用动。

### 5. `Ball.Id` 和 `ball.Name` 是两份

* **当时的做法**：`ball.Name = data.Id`，两颗同种球相撞时引擎把它改成 `@CharacterBody2D@41` 那种，日志没法看。
* **现在**：`Ball.Id` 是**唯一的数据身份**（找素材、找音效都它）；`Name` 只是调试标签，由装配器按序号生成
  （`pulipuli#1`、`Egg2003#1`），日志里一眼知道是第几个。约定已写进 `FunctionGuide`。

### 6. 伤害载荷是裸 `float`（✅ 已处理）

* **当时**：`Ball.TakeDamage(float)` → `Events.Trigger(take_damage, damage)`，没有来源、也没有"剩余伤害"的概念，护盾那类组件根本没法写。
* **现在**：载荷是 `DamageEvent`（`basic/DamageEvent.cs`）：
  * `Amount` 可变 → 减伤/护盾"扣一部分再放行"能写了；`4001` 按它扣血。
  * `Original` 只读 → 按原始伤害算的组件（护盾）有依据。
  * `Source` / `SourceId` / `SourceType` → 能区分"谁打的、哪种攻击"（蛋的伤害记在下蛋那颗球头上）。
  * `TakeDamage` 返回 `true/false` → 调用方知道"这次有没有被挡下来"。
* **还剩**：3xxx 里除了 `3001` 之外的组件（护盾/真伤/沉默）还没做，但**位置已经留好了**（`DamagePriority` 里的 700/800/900）。

### 7. 控制类组件不能共存（✅ 已按你的思路处理）

* **现在**：`BallComponent` 上多了三个属性给控制类用——`ControlCategory` / `ControlCategoryPriority` / `ControlStrength`；
  `BallControl.PickDriver(球)` 每帧按"先比类别优先级、同类别再比强度、最后按挂载顺序"选出一个驱动方。
  **强的那条拿到方向盘，弱的自然就不生效**（弱的那条线还活着，只是球不听它）——就是你说的"阻塞 / 非阻塞那套"。
  `Slow` 已实现：`category` / `category_priority` / `strength`（不显式配就按"减得多狠"折算，0.3 倍 → 强度 70）。
* **还没动**：你说的"减速会影响攻击"（受控期间进不了攻击状态）是设计取舍，先留着。
* **建议**：球那边做个"谁驱动"的登记，按优先级选一个（这也是 `priority` 真正的用武之地）。

### 8. Spine 那一层没有编译期检查

* **位置**：`SpineLook` 全程 `ClassDB.Instantiate` + `Call("方法名")`。
* **原因**：C# 里根本没有 Spine 的类（GDExtension 只对引擎/GDScript 可见），无解——除非把这一层用 GDScript 写。
* **风险**：方法名、属性名写错只在运行时炸；Spine 扩展升级也可能改动 API。
* **建议**：保持这一层薄（现在已经够薄），改的时候一定要跑一次实机。

### 9. 蛋没有自己的数据（✅ 已处理）

* **现在**：蛋有了自己的基类 `Egg`（`basic/Egg.cs`）+ 编号表 `EggLibrary`，**一种蛋一个子类**
  （`eggs/ShitEgg.cs`）。不走数据配置——蛋和蛋差别可能很大（会飘的、会炸的），代码里写更直接。
  加蛋的步骤在 `FunctionGuide` 的「如何新增一种蛋」。

* **位置**：`Shift` 把外观（贴图/尺寸）和行为参数写在自己的默认值里；`EggAttack.egg_id` 只能指定一个组件。
* **后果**：想换一种蛋 = 新写一个组件；蛋的行为参数只能改组件的默认值。
* **建议**：等你要做第二、第三种蛋时，扩成"蛋也读一份数据"。

### 10. 音效的归属（✅ 已处理）

* **现在**：**小球独特资源的找法统一成一条**——`BallLibrary.Find(名字, 球id, 后缀...)`：
  先在球自己的 `user://balls/<球id>/resource/` 找，再找 `user://` 根目录，名字可以不写后缀。
  头像、音效、Spine 三件套、蛋的素材全走它。`SoundTool` 只是它的一个调用方。
  "从 `enemycomponents` 送出去的组件用谁的包"由 `Bind` 的 `configId` 解决（送出去的那颗球）。

* **位置**：`SoundTool.Resolve(sound, ballId)` 按"被挂的那颗球"的目录找文件。
* **现象**：通过 `enemycomponents` 挂给对面的攻击组件，音效会去**对面**的目录找。
* **现状**：现在没人这么用，碰不到；要用之前得先把"配置来自哪颗球"传进去。

### 11. 场景里的数字 vs 数据里的数字（✅ 已处理）

* **已经做的**：
  * 选球确认之后可以**取消重选**（确认键变"重选"，并通知选球页重新禁掉"开始游戏"）。
  * **场地系统**：`user://scenes/<场地id>/` 一个文件夹一个场地（`scenedata.json` + `avatar.png`），
    和球完全同一套规矩（`DataSeeder` 只补缺、`SceneLibrary` 扫描）。场地数据现在**只有出生范围**。
  * **场地预制体**：墙和边框从 `game.tscn` 抽成了 `assets/scene/arenas/<场地id>.tscn`，
    `Game` 按选中的 id 实例化（约定同名，Json 里不写路径）。
  * `GameManager.SceneId` 记住选中的场地；`StartGame(球1, 球2, 场地id)` 不传就默认场地。
  * 纯蓝占位头像已生成（64×64）。
  * **场地选择界面**：`ui/ScenePicker.cs` 在选球页中下方铺一排场地按钮（复用 `BallButton.tscn`，
    界面在 `_Ready` 里搭出来，不用再维护一个场景文件），点中即选、默认第一个；
    `SelectPage` 把选中的 id 传进 `StartGame`。老的 `arena.json` 和 `UserData.Scene` 已删掉。
* ⚠️ **坑**：出生范围别压到墙里——第一次写的范围盖住了左侧墙，球一出生就被挤出去飞到场外（x = -379）。

* **位置**：`Game.cs` 出生点写死 700/1220、墙面写在 `game.tscn`；而 `arena.json` 里是 300/1620。
* **建议**：要么删掉 `arena.json`，要么把它接上（别让它一直是一份"看起来有用其实没读"的数据）。

### 12. `resources/` 在 `res://` 里（✅ 已处理）

* **现在**：草稿素材挪到了 `draft/`，已加进 `.gitignore`，并在里面放了 `.gdignore`
  （引擎完全跳过它，不会导入、不会报错）。`resources/` 目录已清空删除。
  正式素材一律进球自己的 `assets/data/balls/<球id>/resource/`。

* **现象**：你的素材暂存区被引擎扫描并导入（生成了 `.import` 文件）；球正式用的素材应该在 `assets/data/balls/<球id>/resource/`。
* **建议**：`resources/` 只当草稿区，或者干脆挪到工程外。

### 13. 版本管理

* 已经提交过两批：`feat:`（接 Spine 外观 + 一批组件）、`fix:`（修蛋与 UI 层级）。
* 目前工作区里是最近这批（伤害事件、球尺寸、音效、血条等），**审计前先提交一次**，这样你改的时候有基线可比。
* `addons/`（53MB Spine 二进制）一直没入库，等你定：全提交 / 只留 windows 版 / 不入库（不入库的话别人拉下来要自己放扩展，代码有兜底会回落贴图）。

## 八、快速用法（细节看 `FunctionGuide.md`）

| 想干什么 | 怎么做 |
| --- | --- |
| 加一颗球 | 建 `assets/data/balls/<id>/` → 写 `balldata.json` → 放 `resource/avatar.png`（Spine 球再放 atlas+骨架）→ 跑一次让补数据复制 |
| 加一个组件 | `components/` 下写类（**文件名 = 类名**）→ 接线写在 `Bind` 里（注册事件、连信号、改形状）→ `ComponentLibrary` 加一行。**装配器不用动** |
| 加一种蛋 | `eggs/` 下写 `Egg` 的子类（覆盖 `OnHit`）→ `EggLibrary` 加一行 → 球的 `2002.egg_id` 填编号 |
| 编译 | `NUGET_PACKAGES=C:\Users\24807\.nuget\packages` 后 `dotnet build ShineBallGame.csproj`（编辑器的构建按钮不可用） |
| 跑一局看日志 | `godot.windows.editor.x86_64.mono.console.exe --headless --path D:\shine-ball-game res://assets/scene/index.tscn --fixed-fps 60 --quit-after N`（`--fixed-fps` 必须加，否则倒计时/周期逻辑推不动） |
| 直接打一场 | 临时脚本里 `GameManager.StartGame("球A", "球B")` 再跑帧（我这几次的验证都是这么做的） |
| 看 `user://` 在哪 | `%APPDATA%\Liya\app_userdata\ShineBallGame\` |

## 九、我建议的下一步顺序

1. ~~**伤害载荷换成对象**（`DamageEvent`）~~ —— ✅ 已做（见自查第 6 条）
2. ~~**外观层收口**~~ —— ✅ 已做（`BallLook`）。
3. ~~**事件注册统一**~~ —— ✅ 已做（组件自己 `Bind`）。
4. **控制组件抢方向盘**——`Slow` 已经有 `category` 字段，但"同类别按优先级选一个"的仲裁还没做（为"又减速又眩晕"准备）。
5. **场景选择界面**——`arena.json` 还没人读；按你说的做成"和球一样在 `user://` 里组织 + 一个 avatar"，放在选球界面中下方。
6. **蛋的到期消失**——没人碰过的蛋会一直躺着，要不要加"到期自动消失"。
