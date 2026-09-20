using Godot;

/// <summary>
/// **领域**（`8xxx`）的基类：一份配置 + 场上的一个圆。想加新领域就继承它
/// （见 `assets/scripts/components/jujutsu/gojo/UnlimitedVoid.cs`），
/// 规则都在这里，子类只要定配色、换场的样式、想加点自己的表现就覆盖两个钩子。
///
/// 它管四件可配的事（都写在 Json 的参数里）：
///
/// | 参数 | 说明 |
/// | --- | --- |
/// | `open` | **开放型领域**：没有外壳，所以打不碎、也不参与优先级比较；不写就是"非开放" |
/// | `shell_hp` | 外壳能扛多少伤害。**不写就用持有者最大血量的一半**（挨够这么多就把领域打碎） |
/// | `radius` | 半径：外壳/场就在这个半径上，也是场展开的大小 |
/// | `priority` | 优先级：**数字大的压缩数字小的**，差 10 以上直接把对方压到 0（领域当场被覆盖） |
///
/// 剩下的都是"开得起来"所需的常规配置：`duration`（持续几秒，默认 99）、`cost`（开一次花多少咒力）、
/// `windup`（前摇，配合招式动画）、`move`（招式表里这一招叫什么）、`burnout_time`（收场后熔断多久）。
///
/// 几条规则（都在这儿实现，子类不用重复写）：
///
/// 1. **什么时候开**：对手场上出现了敌对领域就**立马跟进**（每帧检查，条件一满足就开）；
///    平时则当成一招——招式表里挑中 `move` 这个名字时，出招的组件照常起手，
///    这里订 `attack_started` 认领那一招，然后展开。两条路最后都走"付账 → 前摇 → 展开"。
/// 2. **外壳怎么掉血**：领域生效期间，持有者挨的伤害记一笔账（在受伤链上排在无敌后面、
///    护盾前面，读 `Original`——被护盾吃掉的那部分也算，被无敌挡下来的不算），
///    攒够 `shell_hp` 就把领域打碎。**伤害照常进持有者的血**，这一层不替人挨打。
/// 3. **范围互相压**：非开放型领域每帧算一次"有效半径"，
///    `半径 × (1 − 优先级差 ÷ 10)`，差 10 以上就是 0 —— 领域当场被对面覆盖。
/// 4. **收场进熔断**：时间到、被打碎、被覆盖，三种都算"领域结束"，
///    让咒力池进入熔断（`burnout_time` 秒）：期间术式停手（`2005` 不出招、`3004` 不灌盾）、
///    平A 掉到熔断那一档。这就是"领域是最后手段"的代价。
///
/// **领域本身不带效果**：它只负责"场上有一个圈、圈在这儿、壳还剩多少、什么时候碎"。
/// 谁在圈里会怎样（无量空处是"定住"）由**给对面的组件**决定，见 `DomainControl`（`8002`）——
/// 和苍/赫牵引（`1002`）是同一种交付方式。所以同一个领域想换效果，不用动这个文件。
/// </summary>
public abstract partial class Domain : BallComponent
{
    /// <summary>球身上那根领域条的节点名（调试、以后想从球上找它时用）。</summary>
    public const string BarName = "DomainBar";

    // ---------- 可配的参数（Json 里同名的键）----------

    /// <summary>领域持续多久（秒）。</summary>
    public float Duration = 99f;

    /// <summary>展开一次花多少咒力（名义值，实际还要乘咒力池的消耗倍率）。</summary>
    public float Cost = 600f;

    /// <summary>开放型领域：没有外壳——打不碎，也不参与优先级/范围的比较。</summary>
    public bool Open;

    /// <summary>外壳能扛多少伤害。**0（不写）就用持有者最大血量的一半**。</summary>
    public float ShellHp;

    /// <summary>半径：场的边界（也是外壳的位置）。</summary>
    public float Radius = 360f;

    /// <summary>优先级：大的压小的；差 10 以上直接覆盖。</summary>
    public int Priority = 10;

    /// <summary>前摇（秒）：进攻击状态之后过这么久才真正展开（留给手势/动画）。</summary>
    public float Windup = 0.6f;

    /// <summary>招式表里这一招叫什么（招式名）。出招那边选中它，这里就展开。</summary>
    public string Move = "domain";

    /// <summary>对手开领域时要不要立刻跟进（跟着开）。</summary>
    public bool Follow = true;

    /// <summary>收场之后术式熔断多久（秒）。</summary>
    public float BurnoutTime = 20f;

    /// <summary>招式表里没配这一招时，攻击状态演多久（配了就用表里的）。</summary>
    public float AttackTime = 0.8f;

    /// <summary>环宽（像素）。</summary>
    public float RingWidth = 2f;

    // ---------- 给外面看的（`8002` 要问"这颗球现在有没有自己的领域"）----------

    /// <summary>这会儿领域开着没有。</summary>
    public bool Active => _active;

    /// <summary>
    /// 这会儿是不是**正在开**（前摇里、还没真正展开）。
    /// 给"领域对撞"用：两边同时开的时候，先展开的那一方不该在对方的前摇里把对面锁死——
    /// 所以判定"我正在用领域顶"时，前摇中也要算数（见 `DomainQuery.Contesting`）。
    /// </summary>
    public bool Pending => _pending;

    private Ball _ball;
    private CursedEnergy _pool;
    private DomainField _field;
    private DomainBar _bar;

    private bool _active;
    private bool _pending;
    private float _windupLeft;
    private float _timeLeft;

    /// <summary>外壳总共能扛多少（`ShellHp`，没配就是最大血量的一半）。</summary>
    private float _shellMax;

    /// <summary>这一轮已经挨了多少（算的是"打进来的伤害"，不是"掉了多少血"）。</summary>
    private float _shellDamage;

    /// <summary>上一次推给场 / 报过日志的半径（被对面的领域压小了就报一句，方便对数）。</summary>
    private float _radiusLogged;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Duration = JsonTool.GetValue(parameters, "duration", Duration);
        Cost = JsonTool.GetValue(parameters, "cost", Cost);
        Open = JsonTool.GetValue(parameters, "open", Open);
        ShellHp = JsonTool.GetValue(parameters, "shell_hp", ShellHp);
        Radius = JsonTool.GetValue(parameters, "radius", Radius);
        Priority = JsonTool.GetValue(parameters, "priority", Priority);
        Windup = JsonTool.GetValue(parameters, "windup", Windup);
        Move = JsonTool.GetValue(parameters, "move", Move);
        Follow = JsonTool.GetValue(parameters, "follow", Follow);
        BurnoutTime = JsonTool.GetValue(parameters, "burnout_time", BurnoutTime);
        AttackTime = JsonTool.GetValue(parameters, "attack_time", AttackTime);
        RingWidth = JsonTool.GetValue(parameters, "ring_width", RingWidth);
    }

    /// <summary>
    /// 装配时接线：拿到球和咒力池、订两条事件、把球身上那根领域条造出来、登记面板那行数。
    /// 和前摇、外壳相关的账都在这儿起头，真正展开要等到开打以后（见 `_PhysicsProcess`）。
    /// </summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，领域展不开。");
            return;
        }

        _pool = FindPool(ball);
        if (_pool == null)
        {
            GD.PushError($"[{Type}] 球上没有 {CursedEnergy.TypeName}，领域展不开。");
            return;
        }

        // 一、外壳的账：挨打就记一笔（详细规则见类说明第 2 条）
        ball.Events.Register(EventName.take_damage, new EventResponseFunction
        {
            priority = DamagePriority.DomainShell,
            action = OnTakeDamage,
        });

        // 二、认领招式：谁在招式表里演了 `Move` 这一招，谁就是在开这个领域
        ball.Events.Register(EventName.attack_started, new EventResponseFunction
        {
            priority = 0,
            action = OnAttackStarted,
        });

        // 三、条：自己造、自己挂进条区（没开领域的时候是隐藏的，不占地方）
        var layout = BallLook.Layout(ball);
        if (layout == null)
        {
            GD.PushError($"[{Type}] 预制体里没有条区（{BallLook.LayoutPath}），领域条挂不上。");
        }
        else
        {
            _bar = new DomainBar { Name = BarName, Visible = false };
            layout.AddChild(_bar);
        }

        // 四、面板那一行数：登记**现算函数**，不是当前值（装配期谁先 Bind 不保证）
        ball.Readouts.Add(new BallReadout(DisplayName, Readout));

        GD.Print($"[{Type}] {ball.Name} 的{DisplayName}就绪：{ShellText}，"
            + $"持续 {Duration:0}s，展开花 {Cost:0} 咒力"
            + (Follow ? "，对手开领域就立刻跟进" : string.Empty));
    }

    /// <summary>进场景树之后把条的显隐落实一次（`Bind` 时球还没进树，条区还没排过）。</summary>
    public override void _Ready()
    {
        if (_bar != null)
        {
            _bar.Visible = _active;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_ball == null || _pool == null)
        {
            return;
        }

        float dt = (float)delta;

        // 人没了，领域跟着散（死在领域里不算"熔断"，人都不在了）
        if (_ball.State == BallState.Dead)
        {
            if (_active || _pending)
            {
                Close("施术者已死", burnout: false);
            }

            return;
        }

        // 前摇：到点就展开
        if (_pending)
        {
            _windupLeft -= dt;
            if (_windupLeft <= 0f)
            {
                _pending = false;
                Expand();
            }

            return;
        }

        if (_active)
        {
            TickActive(dt);
            return;
        }

        // 没开：场上出现敌对领域就立马跟进
        if (Follow && DomainQuery.EnemyDomainUp(_ball))
        {
            TryFollow();
        }
    }

    /// <summary>生效期间：走时长、算有效半径、推给场和条，然后看该不该收场。</summary>
    private void TickActive(float dt)
    {
        _timeLeft -= dt;

        float radius = EffectiveRadius();
        if (radius <= 0.01f)
        {
            Close("被更强的领域覆盖");
            return;
        }

        if (_timeLeft <= 0f)
        {
            Close("时间到");
            return;
        }

        Push(radius);
    }

    /// <summary>把这一帧的半径 / 外壳剩余推给场和球身上的条。</summary>
    private void Push(float radius)
    {
        if (Mathf.Abs(radius - _radiusLogged) > 0.5f)
        {
            // 只在"被压小了/回弹了"的时候报一句：对面开领域、对面领域碎了都会走到这儿
            GD.Print($"[{Type}] {_ball.Name} 的{DisplayName}范围变成 {radius:0}"
                + $"（原本 {Radius:0}，对面的优先级更高时会压小我）");
            _radiusLogged = radius;
        }

        if (_field != null)
        {
            _field.Radius = radius;
            _field.ShellRatio = Open ? 1f : ShellRatio;
        }

        if (_bar != null && _bar.Visible)
        {
            _bar.SetRatio(Open ? _timeLeft / Mathf.Max(Duration, 0.01f) : ShellRatio);
        }
    }

    /// <summary>
    /// 这一帧的有效半径：**优先级大的压小的**，`半径 × (1 − 优先级差 ÷ 10)`，
    /// 差 10 以上是 0（领域被覆盖）。开放型不参与这套比较——既不被压缩，也不压缩别人。
    /// </summary>
    private float EffectiveRadius()
    {
        if (Open)
        {
            return Radius;
        }

        float scale = 1f;

        foreach (var node in GetTree().GetNodesInGroup(DomainField.FieldsGroup))
        {
            if (node is not DomainField field || !field.Active || field.Open || field.OwnerGroup == _ball.Group)
            {
                continue;
            }

            int gap = field.Priority - Priority;
            if (gap <= 0)
            {
                continue; // 对方不强（或者跟我一样强）：压不动我
            }

            scale = Mathf.Min(scale, Mathf.Max(0f, 1f - gap / 10f));
        }

        return Radius * scale;
    }

    /// <summary>
    /// 跟开：对手开领域了，自己也来一发。先演招式（走招式表，动作和动画跟平时出招一致），
    /// 付账和前摇由 `attack_started` 那条线接下去——两条开领域的路最后都汇到 `BeginWindup`。
    /// </summary>
    private void TryFollow()
    {
        if (!CanStart())
        {
            return;
        }

        float duration = Mathf.Max(AttackTime, Windup);
        if (MoveTable.TryGet(_ball.Id, Move, out float tableTime, out _) && tableTime > 0f)
        {
            duration = Mathf.Max(tableTime, Windup);
        }

        _ball.BeginAttack(Move, duration, AttackRepeat.Replace);
    }

    /// <summary>能不能起手（登场中、受控、熔断都不行——那几种情况等下一帧再看）。</summary>
    private bool CanStart()
    {
        if (_active || _pending || _ball.State == BallState.Dead)
        {
            return false;
        }

        if (_ball.State != BallState.Move && _ball.State != BallState.Attack)
        {
            return false; // 登场 / 受控：这会儿起不了手
        }

        return !_pool.BurnedOut;
    }

    /// <summary>招式名对上了：这就是"开领域"这一招，付账、进前摇。</summary>
    private bool OnAttackStarted(object arg)
    {
        if (arg is string move && move == Move)
        {
            BeginWindup();
        }

        return true; // 通知类事件，一律放行
    }

    /// <summary>
    /// 开领域的第一步：**先付账，再前摇**（付不起就不开，也不占这一招）。
    /// 已经展开了、或者正在前摇里，就什么都不做。
    /// </summary>
    private void BeginWindup()
    {
        if (_active || _pending || !CanStart())
        {
            return;
        }

        if (!_pool.TrySpend(Cost))
        {
            GD.Print($"[{Type}] {_ball.Name} 咒力不够，{DisplayName}展不开");
            return;
        }

        _pending = true;
        _windupLeft = Mathf.Max(Windup, 0f);
    }

    /// <summary>
    /// 真正展开：把场造出来挂在球身下（于是"领域跟着小球走"）、切咒力状态、
    /// 把这一轮的外壳账清零、把条亮出来。
    /// </summary>
    private void Expand()
    {
        _active = true;
        _timeLeft = Mathf.Max(Duration, 0.01f);
        _shellMax = Open ? 0f : (ShellHp > 0f ? ShellHp : _ball.MaxHp * 0.5f);
        _shellDamage = 0f;

        _field = CreateField();
        _field.Name = DisplayName;
        _field.Setup(_ball.Group, Radius, Open);
        _field.Priority = Priority;
        _field.RingWidth = RingWidth;
        _field.RingColor = RingColor;
        _field.FillColor = FillColor;
        _ball.AddChild(_field);

        _radiusLogged = Radius;

        // 领域在场 = 咒力池进入"领域中增强"这一档（熔断和它是互斥的两种模式）
        _pool.State = CursedEnergyState.DomainBoost;

        if (_bar != null)
        {
            _bar.Visible = true;
        }

        GD.Print($"[{Type}] {_ball.Name} 展开{DisplayName}：半径 {Radius:0}、优先级 {Priority}、"
            + (Open ? "开放型（没有外壳）" : $"外壳 {_shellMax:0}")
            + $"，持续 {Duration:0}s");
        OnExpanded();
    }

    /// <summary>
    /// 收场：时间到 / 被打碎 / 被覆盖 / 人没了都走这儿。
    /// 场碎掉、条收起来，然后（除非人已经没了）让咒力池熔断。
    /// </summary>
    private void Close(string reason, bool burnout = true)
    {
        bool wasActive = _active;

        _active = false;
        _pending = false;
        _timeLeft = 0f;

        _field?.Shatter();
        _field = null;

        if (_bar != null)
        {
            _bar.Visible = false;
        }

        if (!wasActive)
        {
            return;
        }

        GD.Print($"[{Type}] {_ball.Name} 的{DisplayName}结束了（{reason}）");

        if (burnout)
        {
            _pool?.EnterBurnout(BurnoutTime);
        }

        OnClosed();
    }

    /// <summary>
    /// 受伤链上的外壳账：领域生效期间，挨的伤害攒起来，攒够就把领域打碎。
    /// **记的是 `Original`（打进来多少），不是 `Amount`（还剩多少）**——
    /// 护盾吃掉的那部分也算挨打（不然有盾的家伙领域永远碎不了），
    /// 而真被无敌挡下来的伤害到不了这儿（那个在更前面就阻塞了整条链）。
    /// 不阻塞，伤害照常往后走。
    /// </summary>
    private bool OnTakeDamage(object arg)
    {
        if (!_active || Open || arg is not DamageEvent hit)
        {
            return true;
        }

        _shellDamage += Mathf.Max(hit.Original, 0f);

        if (_shellDamage >= _shellMax)
        {
            Close("外壳被打碎");
        }

        return true;
    }

    /// <summary>外壳还剩多少（用来显示）。</summary>
    private float ShellLeft => Mathf.Max(0f, _shellMax - _shellDamage);

    /// <summary>
    /// **领域把那一下必中吸下来**：对方的领域必中打过来时，先算在保护你的这个领域上，
    /// 而不是打在人身上（见 `8004`）。返回 true = 这一下到此为止，别打到持有者。
    ///
    /// * **非开放型**：记到外壳的账上，攒够就把领域打碎——所以两个领域对撞时，
    ///   谁的壳先撑不住谁先暴露（这就是"领域互相抵消必中"）；
    /// * **开放型没有壳**：不吃伤害，但**照样把必中抵消掉**——这就是开放型的强势之处：
    ///   打不碎它，可它一直替持有者挡着对面的必中。代价是范围压不过别人时会被覆盖。
    /// * 领域还没开着：返回 false（没东西可挡，必中直接落到人身上）。
    ///
    /// 和"持有者挨打记账"（`OnTakeDamage`，受伤链 750 那一格）是两个入口、同一本账：
    /// 那条是"人挨的打"，这条是"领域替人挨的打"。两条的伤害都不进血量。
    /// </summary>
    public bool Absorb(float amount)
    {
        if (!_active)
        {
            return false;
        }

        if (Open || amount <= 0f)
        {
            return true; // 开放型：不吃伤害，但必中被它挡下来了
        }

        _shellDamage += amount;

        if (_shellDamage >= _shellMax)
        {
            Close("外壳被打碎");
        }

        return true;
    }

    /// <summary>外壳还剩多少（0~1）。</summary>
    private float ShellRatio => _shellMax > 0f ? Mathf.Clamp(ShellLeft / _shellMax, 0f, 1f) : 1f;

    /// <summary>面板那一行数：没开就写"未展开"；开了显示还剩几秒、壳还剩多少。</summary>
    private string Readout()
    {
        if (!_active)
        {
            return "未展开";
        }

        return Open
            ? $"剩 {_timeLeft:0}s（开放型，没有外壳）"
            : $"剩 {_timeLeft:0}s　壳 {ShellLeft:0} / {_shellMax:0}";
    }

    /// <summary>日志和面板里描述外壳的那半句话。</summary>
    private string ShellText => Open
        ? "开放型（没有外壳）"
        : (ShellHp > 0f ? $"外壳 {ShellHp:0}" : "外壳 = 最大血量的一半");

    // ---------- 子类可以改的几处 ----------

    /// <summary>场长什么样。想换配色/装饰就覆盖（见 `UnlimitedVoidField`）。</summary>
    protected virtual DomainField CreateField() => new DomainField();

    /// <summary>场的填充色。</summary>
    protected virtual Color FillColor => new Color(0.12f, 0.12f, 0.18f, 0.32f);

    /// <summary>场的环色。</summary>
    protected virtual Color RingColor => new Color(0.85f, 0.85f, 1f, 0.9f);

    /// <summary>展开那一刻（子类想加点表现/状态就覆盖它）。</summary>
    protected virtual void OnExpanded()
    {
    }

    /// <summary>收场那一刻。</summary>
    protected virtual void OnClosed()
    {
    }

    /// <summary>按 `Type` 名在球身上找咒力池——依赖声明的就是这个名字。</summary>
    private static CursedEnergy FindPool(Ball ball)
    {
        foreach (var child in ball.GetChildren())
        {
            if (child is BallComponent component && component.Type == CursedEnergy.TypeName)
            {
                return component as CursedEnergy;
            }
        }

        return null;
    }
}
