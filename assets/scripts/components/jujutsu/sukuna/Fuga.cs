using Godot;

/// <summary>
/// **灶開**（`2009`）：宿儺的火焰。**伤害跟着斩击刻印（`7003`）走**——
/// `base + per_stack × 刻印数`（默认 `2000 + 800/格`：攒到 8 格就是 8400）。
/// 所以"先斩、再开"是它的全部玩法：斩得越多，这一发越狠。
///
/// 它不认识"解"和"捌"，只认识刻印池（按 `Type` 名找得到就行）——那两个也只知道往池子里刻，
/// 谁都不认识谁。想加第三种"能刻印的东西"（领域里的必中斩 `8004` 就是），
/// 不用动这个文件，它照样会吃到那些格。
///
/// 放出去的是**一团会飞的火焰**（`FireArrow`：命中就炸，`splash` 半径内的敌人各挨一下），
/// 伤害在这一发**放出来的那一刻**就按当时的刻印数算死了（飞行途中再涨不算它的）。
/// 放完按 `consume` 决定要不要把刻印清空（默认清——"攒→放"是一个循环）。
/// </summary>
public partial class Fuga : BallComponent
{
    private const string ArrowScene = "res://assets/scene/projectiles/FireArrow.tscn";

    public override int Id => 2009;

    public override string Type => "attack.fuga";

    public override string DisplayName => "灶開";

    public override string Description => "火焰：伤害 = 基础 + 每格刻印加伤 × 刻印数，放完清空刻印";

    /// <summary>前置组件：咒力池（花咒力）和斩击刻印（算伤害的来源）。</summary>
    public override string[] Requirements => new[] { CursedEnergy.TypeName, SlashMark.TypeName };

    /// <summary>基础伤害（一格刻印都没有时的底线）。</summary>
    public float Base = 2000f;

    /// <summary>每一格刻印加多少伤害。</summary>
    public float PerStack = 800f;

    /// <summary>这一发的伤害上限。0 = 不封顶。</summary>
    public float MaxDamage;

    /// <summary>放一次花多少咒力（名义值）。</summary>
    public float Cost = 3000f;

    /// <summary>放完要不要把刻印清空。默认清（不然只会越攒越大）。</summary>
    public bool Consume = true;

    /// <summary>
    /// **刻印少于这么多格就不放**（把这一发留着，等斩够了再开）。
    /// 0 = 不看条件，有几格刻印就按几格打。
    /// </summary>
    public int MinStacks = 5;

    /// <summary>火焰飞多快（像素/秒）。</summary>
    public float Speed = 1500f;

    /// <summary>飞多久还没打到人就消失（秒）。</summary>
    public float Life = 3f;

    /// <summary>火焰多大（像素）。</summary>
    public float Size = 220f;

    /// <summary>炸开的半径（像素）。0 = 只打命中的那一个。</summary>
    public float Splash = 160f;

    /// <summary>招式表里这一招叫什么。</summary>
    public string Move = "fuga";

    private Ball _ball;
    private CursedEnergy _pool;
    private SlashMark _marks;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Base = JsonTool.GetValue(parameters, "base", Base);
        PerStack = JsonTool.GetValue(parameters, "per_stack", PerStack);
        MaxDamage = JsonTool.GetValue(parameters, "max_damage", MaxDamage);
        Cost = JsonTool.GetValue(parameters, "cost", Cost);
        Consume = JsonTool.GetValue(parameters, "consume", Consume);
        MinStacks = JsonTool.GetValue(parameters, "min_stacks", MinStacks);
        Speed = JsonTool.GetValue(parameters, "speed", Speed);
        Life = JsonTool.GetValue(parameters, "life", Life);
        Size = JsonTool.GetValue(parameters, "size", Size);
        Splash = JsonTool.GetValue(parameters, "splash", Splash);
        Move = JsonTool.GetValue(parameters, "move", Move);
    }

    /// <summary>
    /// 这一招现在能不能用：**刻印够 `min_stacks` 才放**。
    /// 出招器会先问这一句，不够就把这一招剔出这一轮（改放别的招），
    /// 而不是"放出来又收回"；`OnAttackStarted` 里那句检查是兜底。
    /// </summary>
    public override bool CanUse(string moveId)
    {
        if (string.IsNullOrEmpty(moveId) || moveId != Move)
        {
            return true;
        }

        return _marks != null && _marks.Count >= MinStacks;
    }

    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，灶開放不出来。");
            return;
        }

        _pool = Find<CursedEnergy>(ball, CursedEnergy.TypeName);
        _marks = SlashMark.Find(ball);
        if (_pool == null || _marks == null)
        {
            GD.PushError($"[{Type}] 球上没有 {CursedEnergy.TypeName} 或 {SlashMark.TypeName}，灶開放不出来。");
            return;
        }

        ball.Events.Register(EventName.attack_started, new EventResponseFunction
        {
            priority = 0,
            action = OnAttackStarted,
        });
    }

    private bool OnAttackStarted(object arg)
    {
        if (_ball == null || _pool == null || _marks == null || arg is not string move || move != Move)
        {
            return true;
        }

        var target = DamageProjectile.NearestEnemyOf(_ball, _ball.Group, _ball.GlobalPosition);
        if (target == null)
        {
            GD.Print($"[{Type}] {_ball.Name} 找不到对手，灶開空放");
            return true;
        }

        // 刻印太少就先留着：这一发是"攒够了才开"的，不是随时都能点
        if (_marks.Count < MinStacks)
        {
            GD.Print($"[{Type}] {_ball.Name} 只有 {_marks.Count} 格刻印（要 {MinStacks} 格），"
                + "这一发留着，继续斩");
            return true;
        }

        if (!_pool.TrySpend(Cost))
        {
            GD.Print($"[{Type}] {_ball.Name} 咒力不够，灶開放不出来");
            return true;
        }

        // 伤害在放出来的这一刻算死：刻印几格就是几格
        int stacks = _marks.Count;
        float damage = Base + PerStack * stacks;
        if (MaxDamage > 0f)
        {
            damage = Mathf.Min(damage, MaxDamage);
        }

        var parent = _ball.GetParent();
        var scene = parent == null ? null : GD.Load<PackedScene>(ArrowScene);
        if (scene == null)
        {
            GD.PushError($"[{Type}] 球不在场景里、或者找不到火焰的预制体：{ArrowScene}");
            return true;
        }

        var arrow = scene.Instantiate<FireArrow>();
        arrow.Name = DamageProjectile.Numbered("灶開");
        arrow.Position = _ball.Position;
        arrow.Launch(_ball, damage, Type,
            DamageProjectile.LeadDirection(_ball.GlobalPosition, target, Speed),
            Speed, Life, Size, Splash);
        parent.AddChild(arrow);

        if (Consume)
        {
            _marks.Consume();
        }

        GD.Print($"[{Type}] {_ball.Name} 灶開：{stacks} 格刻印 → {damage:0} 伤害（{Base:0} + {PerStack:0}×{stacks}）");
        return true;
    }

    /// <summary>按 `Type` 名在球身上找一个组件。</summary>
    private static T Find<T>(Ball ball, string type) where T : BallComponent
    {
        foreach (var child in ball.GetChildren())
        {
            if (child is T component && component.Type == type)
            {
                return component;
            }
        }

        return null;
    }
}
