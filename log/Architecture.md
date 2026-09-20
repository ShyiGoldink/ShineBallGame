# 项目结构与用法报告

> 这份是给"审计"用的：把**文件、数据流、依赖方向**摊开，最后单列一节
> **「耦合与偏离自查」**——那些做完之后我自己觉得可疑、需要你拍板的地方。
> 日常怎么用（加球、加组件、约定与坑）在 `FunctionGuide.md`，这里不重复。
>
> 结论先放这儿：**功能是通的（实机跑过）**。上一版点名的 3 处结构性耦合
> （装配器认识每个组件 / 外观有四个入口 / 事件注册两种写法）**已经处理掉了**，
> 见下方自查第 1、2、3 条——现在"加组件"不用动装配器、"加外观"只有一处入口、
> 事件一律组件自己注册。剩下的都是具体坑。
>
> 这一版（2026-09-20）新增的是**咒力那一层和领域**：`7001` 咒力池（状态 / 熔断 / 黑闪层数）
> + 五条悟的苍赫与虚式茈 + **领域基类 `Domain` 与无量空处**。领域那一套的拆法和取舍看自查第 15 条，
> 跑测试踩的坑看第 16 条。

## 一、现在是什么

| 项 | 数 |
| --- | --- |
| C# 文件 | 79 个，约 7260 行 |
| 场景 | 9 个（`index` / `game` / `NormalBall` / `BallPicker` / `BallButton` / `BallDataPanel` / `arenas/basic` / `orbs/Orb` / `projectiles/Purple`） |
| 球 | 5 颗（`NormalBall` 贴图球、`pulipuli` Spine 球、`ShieldBall` 盾球、`Gojo` 五条悟、`Sandbag` 沙包） |
| 组件 | 25 个：移动 2 / 攻击 5 / 防御 4 / 行为 7 / 形态 1 / 控制 2 / 咒力 2 / 领域 2（见 `FunctionGuide` 的组件总表） |
| 蛋 | 1 种（`2003` 屎蛋，是 `Egg` 的子类，不是组件） |
| 外部依赖 | Godot 4.7.1（带 C# 的自编译版）+ Spine GDExtension（`addons/spine-godot`） |

玩法一句话：选球 → 3 秒倒计时 → 两颗球按组件打（碰撞、下蛋、减速、中毒）→ 只剩一个阵营时结算。

## 二、目录结构（文件级）

| 路径 | 放什么 | 谁在用 |
| --- | --- | --- |
| `assets/scripts/basic/Ball.cs` | 小球本体：血量、五个状态、受控/攻击的计时、事件总线入口 | 所有组件、装配器、面板 |
| `assets/scripts/basic/BallEvent.cs` | 轻量事件总线：按优先级排、返回 false 阻塞 | `Ball.Events`，各组件注册 |
| `assets/scripts/basic/BallReadout.cs` | 面板读数：标签 + 现算函数（组件登记，面板来拉） | `Ball.Readouts`、`BallDataPanel`、`3003` |
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
| `assets/scripts/components/basic/` | 基础通用组件，按 `movement` / `attack` / `defense` / `status` / `shape` 分小类 | 由装配器按 Json 编号创建 |
| `assets/scripts/components/jujutsu/cursed_energy/` | 咒力底层（咒力池、咒力条、反转术式） | 由装配器按 Json 编号创建 |
| `assets/scripts/components/jujutsu/basic/` | 咒术的通用机制：领域基类 `Domain`、领域效果 `DomainControl` | 同上 |
| `assets/scripts/components/jujutsu/gojo/` | 五条悟专属（苍/赫牵引、术式出招、虚式茈、无下限、无量空处） | 同上 |
| `assets/scripts/domains/` | 领域在场上长什么样：`DomainField`（圈 + 环 + 进出判定）+ 各领域的配色子类 | `Domain.Expand` 造出来、挂在施术者身下 |
| `assets/scripts/orbs/` `assets/scripts/projectiles/` | 场上别的"不是球"的东西：苍/赫球、茈球 | `2005` / `2006` 放出去 |
| `assets/scripts/components/basic/ComponentLibrary.cs` | 编号 → 组件实例的翻译表 | `BallAssembler` |
| `assets/scripts/tool/BallData.cs` | `balldata.json` 的内存模型 | 装配器、`Game` |
| `assets/scripts/tool/BallLibrary.cs` | 扫 `user://balls` 列出所有球、读图；`Find` 是**小球独特资源的统一找法** | `SelectPage`、`Egg`、`SoundTool`、`BallLook` |
| `assets/scripts/tool/DataSeeder.cs` | 首次/每次启动把仓库默认数据补到 `user://`（**只补缺不覆盖**） | `Bootstrap` |
| `assets/scripts/tool/UserData.cs` | `user://` 目录布局常量 | 几乎所有碰文件的地方 |
| `assets/scripts/tool/JsonTool.cs` | 通用 Json 读工具（带缓存、点号取子键） | 所有读 Json 的地方 |
| `assets/scripts/tool/SoundTool.cs` | 读音频（走 `BallLibrary.Find`）、缓存、播一遍 | `2001` 撞击音、`4003` 进状态音 |
| `assets/scripts/tool/Bootstrap.cs` | autoload：启动时补数据 | 引擎启动 |
| `assets/scripts/tool/MoveTable.cs` | 招式表的唯一读法（招式名 → 演多久 + 配哪段动画） | 出招的组件、`SpineLook` |
| `assets/scripts/ui/` | 界面：页面管理、选球、数据面板、血条、护盾条、咒力条、领域条、暂停面板、结算提示 | `index.tscn` / `game.tscn` |
| `assets/scene/*.tscn` | 场景与预制体（`NormalBall.tscn` 是所有球共用的预制体） | 引擎 |
| `assets/data/balls/<球id>/` | 球的**默认数据**（`balldata.json` + `resource/`） | 被补数据复制到 `user://` |
| `assets/data/scenes/<场地id>/` | 场地的**默认数据**（`scenedata.json` + `avatar.png`） | 被补数据复制到 `user://`；墙和边框是同名预制体 `assets/scene/arenas/<场地id>.tscn` |
| `assets/theme/ui_theme.tres` | 中文字体主题 | 所有界面 |
| `draft/` | 草稿素材的暂存区（已 gitignore，里面有 `.gdignore`，引擎不扫描） | 谁都可以往里扔临时素材；正式素材一律进球自己的 `resource/` |
| `addons/spine-godot/` | Spine GDExtension（全平台二进制；**入库了 26 个文件**，`web/` 那几个 wasm 还没入） | `SpineLook` 动态调用；只留 windows 版还是全提交，待定 |
| `.tmp/` | 我本机的临时脚本/验证产物（全局 gitignore 忽略；**注意 `.tmp` 里的 `.cs` 不会进编译**，要跑临时脚本就用 GDScript） | 只有调试用 |
| `log/` | 文档（本文件、`FunctionGuide.md`、`README.md`） | 人 |

## 三、一局的完整生命周期

1. **启动**：引擎跑 autoload → `Bootstrap._Ready` → `DataSeeder.SeedMissing()` 把 `res://assets/data/` 的缺失文件补进 `user://`（只补缺，不覆盖）。
2. **主界面**：`index.tscn` → `IndexManager` 把直接子节点当页面（`Index` / `Select`）。
3. **选球**：`SelectPage._Ready` → `BallLibrary.LoadAll()` 扫 `user://balls/` → 左右两个 `BallPicker` 各铺一排 `BallButton`；双方确认后才放开"开始游戏"。
4. **开局**：`GameManager.StartGame(id1, id2)` 记下选择 → 切 `game.tscn`。
5. **装配**（`Game._Ready`）：`BallData.Load` 读配置 → `BallAssembler.Build` ×2：建球 → 按 `type` 换外观（贴图 / Spine）→ 按编号建自己的组件、查前置组件（`Requirements`）、全挂到球上，再挨个 `Bind` 接线 → `AddChild` → 互相把 `enemycomponents` 挂到对面身上。
6. **倒计时 3 秒**：两颗球都在**登场**状态（`NormalMove` 不动手，Spine 播 idle）。
7. **开打**：`FinishSpawn()` → 进**移动**状态 → 球每帧发一次 `move_step` 事件，由**移动链**按优先级选一个人来驱动（苍/赫 在场时是 `1002`，否则是 `1001`）。
8. **打起来**：`HitArea`（`Area2D`）检测到对方 → `TakeDamage(值)` → `BallEvent` 按优先级跑受伤链（无敌 1000 → 沉默 900 → 真伤 800 → 领域外壳 750 → 护盾 700 → 中毒染色 600 → 一般扣血 500，任何一环返回 false 就截断）。
9. **术式那一套**：出招（`2005`）按权重随机挑一招，苍/赫 落在场上当"术式球"（`JujutsuOrb`，不是球、不进 `balls` 组）；场上凑齐自己的一苍一赫就自动开虚式茈（`2006`）；烧的是咒力池（`7001`）里的咒力，池子的状态（正常 / 领域中增强 / 术式熔断）只有一份。
10. **领域**：出招挑中 `domain`、或者**对手先开了领域** → 领域组件（`8001`）付账 → 前摇 → 展开（场是施术者的子节点，跟着球走）；生效期间持有者挨的伤害记在外壳上，攒够半血就碎；两个领域碰头时按优先级压缩半径、差 10 以上直接覆盖；收场一律熔断（`burnout_time` 秒）。**被罩住的人**由送过去的 `8002` 判定并定身，能不能还手看有没有 `6002`。
11. **对局中按 Esc**：暂停——整棵树冻住，`PauseMenu` 面板（`ProcessMode = Always`）上有"继续 / 返回主菜单 / 退出游戏"；再按 Esc 或点"继续"解冻。结算之后 Esc 不接管（那时是提示的"按任意处退出"）。
12. **结算**：`Game._Process` 每帧看 `balls` 组里还有几个阵营活着 → 只剩一个就显示结果、`GetTree().Paused = true`、亮"按任意处退出"。
13. **回主菜单 / 退出**：`ResultPrompt` 收到输入 → `GameManager.ReturnToMenu()`（先解暂停再切场景）；主菜单上有"退出游戏"按钮，主菜单按 Esc 也能退出。

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
BallAssembler ──→ components(ComponentLibrary 的编号表 + BallComponent 基类)
              └─→ tool(UserData, BallLibrary, SoundTool)
components ──→ basic(Ball, BallMovement, BallEvent, EventName, DamagePriority, DamageEvent)
            └─→ tool(JsonTool, BallLibrary, UserData, SoundTool)
领域(Domain) ──→ basic(BallComponent, Ball, EventName, DamagePriority, CursedEnergyState)
            └─→ domains(DomainField) / ui(DomainBar) / tool(MoveTable, JsonTool)
domains(DomainField) ──→ 只画圈 + 回答"这个点在不在圈里"，不认识任何领域
货真价实的"效果"（8002）──→ domains(找圈) + basic(BallControl, Ball)，不认识领域本体
basic(Ball) ──→ 只认识 BallEvent/BallState，不认识任何具体组件
```

**往上**的依赖只剩"编号表"那一层：装配器只认识 `ComponentLibrary`（编号 → 组件的那张翻译表）和 `BallComponent` 基类，
**不认识任何具体组件**——组件自己接线（`Bind`），组件们反过来也不认识装配器。
代价是**每加一个组件要在 `ComponentLibrary` 补一行**；那是翻译表，本来就该在那儿改（见自查第 1 条）。

## 六、现状清单

| 系统 | 状态 | 备注 |
| --- | --- | --- |
| 选球 / 开局 / 倒计时 / 结算 / 暂停 / 回主菜单 / 退出游戏 | ✅ 实机通过 | 对局中 Esc 暂停（继续 / 返回主菜单 / 退出游戏）；主菜单有"退出游戏"按钮，Esc 也能退 |
| 五状态状态机 | ✅ 都在用 | 受控、攻击的计时在 `Ball` 里 |
| 事件总线（优先级 + 阻塞） | ✅ 实机通过 | |
| 组件（25 个，见组件总表） | ✅ 都跑过 | 移动 2 / 攻击 5 / 防御 4 / 行为 7 / 形态 1 / 控制 2 / 咒力 2 / 领域 2 |
| 组件依赖（`Requirements`） | ✅ 实机通过 | 装配时按 `Type` 名检查，缺前置组件就不挂那个组件并报错；跟 Json 书写顺序无关 |
| 面板自定义读数（`Ball.Readouts`） | ✅ 实机通过 | 组件登记"标签 + 现算函数"，面板每 0.1 秒现拉；`3003` 护盾条、`7002` 咒力、`8001` 领域都是现成例子 |
| 护盾条（球身上那根） | ✅ 实机通过 | `3003` 每帧推值；位置 / 显隐在 `BallLook`（外观唯一入口），青色 8 像素条摆在血条正下方 |
| 外观：`type=1` 贴图 / `type=2` Spine | ✅ 实机通过 | Spine 按状态切动画，缺动画走兜底链 |
| 音效 | ⚠️ 只接了 `2001` / `4003` | `SoundTool` 通用（还支持 `pitch` 调音高：同一份素材能做沉闷版），别的组件想用得自己调 |
| 控制 | ✅ 实机通过 | `6001` 减速（受控期间接手，30%）、`6002` 受控禁手（受控期间关掉自己的命中圈）。同一帧只有一个驱动者（`BallControl.PickDriver`） |
| 防御类 | ✅ `3001` 条件无敌、`3002` 护盾、`3004` 无下限 | 真伤 / 沉默还只有优先级位 |
| 咒力那一层 | ✅ 实机通过 | `7001` 池子（咒力 / 恢复 / 熔断 / 黑闪层数与加成表 / 领域状态）+ `7002` 咒力条；`4005` 反转术式、`2004` 平A（黑闪与熔断分档）、`2005` 出招、`3004` 无下限都站在它上面 |
| 术式（苍/赫/茈） | ✅ 实机通过 | `2005` 按权重随机出招、`2006` 凑齐苍赫自动放茈；苍/赫球与茈球都是"不是球"的独立节点（不进 `balls` 组、不参与胜负） |
| 移动链 | ✅ 实机通过 | 球每帧发 `move_step`，`1002` 苍/赫牵引（优先级 100）会整段接管、否则 `1001` 普通移动（0）；**同一帧只有一个驱动者** |
| 领域（`8001` 无量空处 + 基类 `Domain`） | ✅ 实机 + 截图验过 | 开法两条（出招随机 / 对手开就跟）、外壳记"打进来的伤害"（半血碎）、优先级压范围（差 10 覆盖）、收场熔断；场跟着球走、2px 环是画的、进出用圆心距离 |
| 领域的效果 | ✅ 实机通过 | `8002` 判定"站在敌方领域里且自己没有领域"→ 定身（`duration` 默认 100000s = 这一局废了）；`6002` 负责"不能还手" |
| 毒无视护盾（`4004`） | ✅ 实机通过 | 攻击方写在自己的 `enemycomponents` 里；实测盾球挨它时护盾吸收 0、毒伤全进血 |
| 伤害载荷 | ✅ `DamageEvent` | 带来源、可被中途改写（`Amount`）；扣血统一在 `4001` 并打 `[伤害]` 日志（例外：`4004` 毒血自己扣，为了让毒绕过护盾） |
| 场地（`user://scenes` + `assets/scene/arenas/<id>.tscn`） | ✅ 实机通过 | 数据只管出生范围，墙和边框按 id 找同名预制体 |
| `BallState.Attack` 的"动作" | ✅ 下蛋 / 出招 / 茈 / 领域都在用 | 招式名走 `attack_started`，`AttackRepeat` 管换招/续时长/打断 |
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
* **还剩**：3xxx 里除了 `3001`/`3002` 之外的组件（真伤/沉默）还没做，但**位置已经留好了**（`DamagePriority` 里的 800/900）。

### 7. 控制类组件不能共存（✅ 已按你的思路处理）

* **现在**：`BallComponent` 上多了三个属性给控制类用——`ControlCategory` / `ControlCategoryPriority` / `ControlStrength`；
  `BallControl.PickDriver(球)` 每帧按"先比类别优先级、同类别再比强度、最后按挂载顺序"选出一个驱动方。
  **强的那条拿到方向盘，弱的自然就不生效**（弱的那条线还活着，只是球不听它）——就是你说的"阻塞 / 非阻塞那套"。
  `Slow` 已实现：`category` / `category_priority` / `strength`（不显式配就按"减得多狠"折算，0.3 倍 → 强度 70）。
* **还没动**：你说的"减速会影响攻击"（受控期间进不了攻击状态）是设计取舍，先留着。
* **注意**：仲裁的是"每帧谁来驱动"；"接管那一刻压速度"仍然是每个控制类组件各做一次，
  所以同一颗球真的挂两个减速会叠乘（0.3 × 0.3），别这么配。

### 8. Spine 那一层没有编译期检查

* **位置**：`SpineLook` 全程 `ClassDB.Instantiate` + `Call("方法名")`。
* **原因**：C# 里根本没有 Spine 的类（GDExtension 只对引擎/GDScript 可见），无解——除非把这一层用 GDScript 写。
* **风险**：方法名、属性名写错只在运行时炸；Spine 扩展升级也可能改动 API。
* **建议**：保持这一层薄（现在已经够薄），改的时候一定要跑一次实机。

### 9. 蛋没有自己的数据（✅ 已处理）

* **现在**：蛋有了自己的基类 `Egg`（`basic/Egg.cs`）+ 编号表 `EggLibrary`，**一种蛋一个子类**
  （`eggs/ShitEgg.cs`）。不走数据配置——蛋和蛋差别可能很大（会飘的、会炸的），代码里写更直接。
  加蛋的步骤在 `FunctionGuide` 的「如何新增一种蛋」。

* **位置**：蛋的外观（贴图/尺寸）和行为参数写在蛋类自己的字段默认值里；`EggAttack.egg_id` 只能指定一种蛋。
* **后果**：想换一种蛋 = 新写一个 `Egg` 子类；蛋的行为参数只能改那个类的默认值。
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

* 提交节奏：`feat:` / `fix:` 每批一提交，别攒着。历史里那批"咒力基础 + 无下限"是上一轮，
  这一轮（组件目录分成 `basic` / `jujutsu`、苍/赫与茈、咒力底层、**领域**）是一批一次性进来的。
* `addons/spine-godot/` 入库了 26 个文件（`web/` 那几个 wasm 没入）——**不是"完全没入库"**，
  之前文档里那句是错的，已改。要不要全提交仍然待定。

### 14. 组件依赖（`Requirements`）之前没人检查（✅ 已处理）

* **由来**：`Requirements` 是"自描述"里唯一没人读的字段——写不写、写对写错，装配器都不看。
  加 `3003` 护盾条（它得有护盾才有东西可显示）时，顺手把它补成了真机制。
* **现在**：装配器按 `Type` 名检查——球上已经有、或者这一批里正准备挂的，就算满足；
  缺一个就**报错并且不挂这个组件**（它自己声明了"没有那个就跑不起来"）。
* **顺序上的坑（自己踩的）**：检查本来就跟书写顺序无关，但组件是在自己的 `Bind` 里按 `Type`
  去球上找前置组件的——那会儿球上只挂了"排在前面"的那些，于是把 `3003` 写在 `3002` 前面就找不到。
  现在装配改成"这一批**先全挂到球上**、再挨个 `Bind`"，`Bind` 里找兄弟组件才真的跟顺序无关。
* **还没管**：跨球的依赖（自己身上的组件依赖对面 `enemycomponents` 送来的组件）——两边分两批挂，
  后挂的那批更晚，现在算不出来。
* **顺带验的**：护盾条登记的是"现算函数"而不是当时的值，所以哪怕它的 `Bind` 跑在护盾的 `Bind`
  前面（那一刻盾量还是 0），面板拉到的也是真值——这就是读数表"现问现取"的意义。

### 15. 领域：一套规则、两层拆开（✅ 本轮新做的，四组对照跑过）

* **拆法**：`Domain`（`components/jujutsu/basic/Domain.cs`）管配置和账（什么时候开、外壳、优先级、
  时长、熔断），`DomainField`（`assets/scripts/domains/`）只管"场上那个圈长什么样 + 这个点在不在圈里"，
  具体领域（`UnlimitedVoid`）只填默认值和配色。**效果**再单独一层：`8002 domain.control`
  挂在受害者身上判"被罩住"，`6002` 管"受控时不能还手"。所以同一个领域换效果不用动领域本体。
* **验过的四组**（headless 跑帧 + 截图）：
  1. 必开的配置下正常展开、面板出现"无量空处"那一行、咒力状态变「领域中增强」；
  2. 五条(20) vs 宿傩(30)：差 10 → 五条的领域"被更强的领域覆盖"、当场碎、进熔断，
     之后（没有领域护着）被 `8002` 压住；宿傩因为自己也有领域，**没被无量空处吃到**——和设计里举的例子一致；
  3. 外壳配 200、并且不给对面 `8002`（让它能还手）：攒够伤害 → "外壳被打碎" → 熔断 20 秒 → 回「正常」→ 能再开；
  4. 五条自己**没有**领域这一招（`skills` 里只有 `blue_red`）、宿傩优先级 15：
     宿傩开出来之后五条**立刻跟进**（跟进那条路走通了），宿傩的半径被压成 190（380 的一半，符合 `1 − 5/10`）。
* **取舍（都在 `FunctionGuide` 的约定里）**：外壳记的是"打进来的伤害"（`Original`，护盾吃掉的也算），
  不然有无下限的球永远碎不了；环是画出来的、不做物理碰撞体（Godot 没有环形碰撞形状，
  真做成实心对方就进不来了）；领域跟着球走，所以效果按"站在圈里"每帧判定，不按"碰到"触发。
* **已知的粗糙**：① `2005` 不知道"这一招现在能不能用"，领域开着时再抽到 `domain` 是空放（白花它那份 `cost`）；
  ② `8002` 的 `release_when_free` 现在没有反例用（无量空处配的是锁死），这条分支只在代码里；
  ③ 开放型领域（`open: true`）还没人用，只按"打不碎、不参与优先级比较、只按时间到期"实现着。

### 16. 跑测试的姿势（本轮的踩坑）

* **`.tmp/` 里的 `.cs` 不会进编译**（`.NET SDK` 的默认 glob 不扫隐藏目录），
  所以临时开局的脚本要用 **GDScript** 写：`.tmp/domain_test.gd` 里 `GameManager.call("StartGame", "Gojo", "Sandbag")`，
  再用 `--fixed-fps 60 --quit-after N` 跑帧（`--fixed-fps` 必须加，否则周期逻辑推不动）。
* **切场景会把当前场景的脚本节点一起换掉**：`await` 挂在那个节点上就再也回不来（第一次写截图脚本就卡死在这儿）。
  计时/收尾要挂在 `SceneTree` 上（`tree.create_timer` + `static func`），别挂在当前场景的节点上。
* **headless 跑不了 `user://` 之外的写入**（沙箱里会崩在写日志那一步）——这是本机 sandbox 的事，不是游戏的问题。
* **退出时那句 `N ObjectDB instances were leaked at exit` 是老的**：拿两颗不带领域的球跑一局一样有，
  跟这一轮的改动无关，先记在这儿。

## 八、快速用法（细节看 `FunctionGuide.md`）

| 想干什么 | 怎么做 |
| --- | --- |
| 加一颗球 | 建 `assets/data/balls/<id>/` → 写 `balldata.json` → 放 `resource/avatar.png`（Spine 球再放 atlas+骨架）→ 跑一次让补数据复制 |
| 加一个组件 | `components/` 下写类（**文件名 = 类名**）→ 有前置组件就在 `Requirements` 里写它的 `Type` 名 → 接线写在 `Bind` 里（注册事件、连信号、改形状）→ `ComponentLibrary` 加一行。**装配器不用动** |
| 加一种蛋 | `eggs/` 下写 `Egg` 的子类（覆盖 `OnHit`）→ `EggLibrary` 加一行 → 球的 `2002.egg_id` 填编号 |
| 加一种领域 | `components/jujutsu/<角色>/` 下写 `Domain` 的子类（填默认值、换外观）→ `ComponentLibrary` 加一行 → 球的 `selfcomponents` 写编号 + `attacks` 配招式名 + `2005.skills` 给权重；"对别人干什么"另外写/复用一个送进 `enemycomponents` 的组件（如 `8002`） |
| 编译 | `NUGET_PACKAGES=C:\Users\24807\.nuget\packages` 后 `dotnet build ShineBallGame.csproj`（编辑器的构建按钮不可用） |
| 打包成 exe | `godot.windows.editor.x86_64.mono.console.exe --headless --path D:\shine-ball-game --export-release "Windows Desktop" build/ShineBallGame.exe`，产物在 `build/`（前提见下面两条） |
| 跑一局看日志 | `godot.windows.editor.x86_64.mono.console.exe --headless --path D:\shine-ball-game res://assets/scene/index.tscn --fixed-fps 60 --quit-after N`（`--fixed-fps` 必须加，否则倒计时/周期逻辑推不动） |
| 直接打一场 | 临时脚本里 `GameManager.StartGame("球A", "球B")` 再跑帧（我这几次的验证都是这么做的） |
| 看 `user://` 在哪 | `%APPDATA%\Liya\app_userdata\ShineBallGame\` |

**打包的两个前提**（缺一个就出问题）：

1. **导出模板必须是引擎自己的 .NET 版**，不能是官网标准版——标准版里没有 C# 模块，导出物一启动就满屏 `No loader found for resource: ... .cs`。编法：`scons platform=windows target=template_release arch=x86_64 module_mono_enabled=yes accesskit=no d3d12=no`（debug 把 `target` 换成 `template_debug`），编完把 `bin/godot.windows.template_{release,debug}.x86_64.mono{,.console}.exe` 改名成 `windows_{release,debug}_x86_64{,_console}.exe` 放进 `%APPDATA%\Liya\export_templates\4.7.1.stable\`（官方的标准模板当时是改名成 `*.standard.bak` 备份的，要换回来还认得）。
2. **.NET SDK 10**：编辑器里的发布要它（报 `The required version is '10.0.9'`，按**大版本**匹配，任意 10.x 都行）。没装的话导出只会 "completed with warnings"，**包里带的还是上一次的程序集**——改了代码也进不了包。临时替代办法（用现有的 SDK 9）：导出前手动发一次 `dotnet publish ShineBallGame.csproj -c ExportDebug -r win-x64 --self-contained true -o .godot/mono/temp/bin/ExportDebug/win-x64`，再把 `-o` 那个目录整个同步进 `build/data_ShineBallGame_windows_x86_64/`。

## 九、我建议的下一步顺序

1. ~~**伤害载荷换成对象**（`DamageEvent`）~~ —— ✅ 已做（见自查第 6 条）
2. ~~**外观层收口**~~ —— ✅ 已做（`BallLook`）。
3. ~~**事件注册统一**~~ —— ✅ 已做（组件自己 `Bind`）。
4. ~~**控制组件抢方向盘**~~ —— ✅ 已做（`BallControl.PickDriver`，见自查第 7 条）。
5. ~~**场景选择界面**~~ —— ✅ 已做（`ScenePicker` + `user://scenes`，见自查第 11 条）。
6. ~~**领域**~~ —— ✅ 已做（基类 `Domain` + 无量空处 `8001` + 效果 `8002`/`6002`，见自查第 15 条）。
7. ~~**咒力那一层**~~ —— ✅ 已做（`7001`/`7002` + 无下限 `3004` + 反转术式 `4005` + 黑闪与熔断）。
8. **宿傩那类"靠回血扛得住"的领域**——`open: true`（没有外壳、打不碎）的分支已经实现但没人用：
   要的是"圈里持续掉血、回血跟得上就扛得住"，也就是再写一个 `Domain` 子类 + 一个送对面的伤害组件。
9. **蛋的到期消失**——没人碰过的蛋会一直躺着，要不要加"到期自动消失"。
10. **回盾的机制**——`3002` 的 `regen` 是恒定速率，跟对手的伤害频率一比就是条硬线（实测回盾 12 → 盾球赢 3/9，14 → 4/6），
   想"势均力敌但不脆"得换成"破盾后隔 N 秒才开始回"这类机制，见 `FunctionGuide` 的 `3002` 一节。
