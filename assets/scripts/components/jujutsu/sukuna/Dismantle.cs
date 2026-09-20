using Godot;

/// <summary>
/// **解**（`2007`）：宿儺的那把"普通斩"。**伤害是固定的**，但一次出**两刀**——
/// 两刀之间有 `gap` 秒的间隔（就是"快速斩两刀"的那个节奏），每刀各放一道飞行斩击。
///
/// 它不认识"灶開"、也不认识"捌"：斩击是飞行物（`SlashProjectile`）自己飞、自己判定、
/// 命中之后自己给主人刻一格刻印（`7003`）。这里只干三件事：付账、朝最近的人放刀、记着第二刀什么时候放。
///
/// 怎么被触发：招式表里挑中 `move`（默认 `dismantle`）这一招时，出招器（`2005`）照常起手，
/// 它订的 `attack_started` 认领这一招，然后执行。所以**加一刀不用改出招器**。
/// </summary>
public partial class Dismantle : BallComponent
{
    private const string SlashScene = "res://assets/scene/projectiles/Slash.tscn";

    public override int Id => 2007;

    public override string Type => "attack.dismantle";

    public override string DisplayName => "解";

    public override string Description => "固定伤害的斩击，一次快速出两刀；每刀命中都给主人刻一格斩击刻印";

    /// <summary>前置组件：出招花咒力（池子），命中记刻印（刻印池）。</summary>
    public override string[] Requirements => new[] { CursedEnergy.TypeName, SlashMark.TypeName };

    /// <summary>一次出几刀。</summary>
    public int Hits = 2;

    /// <summary>每一刀的伤害（固定）。</summary>
    public float Damage = 800f;

    /// <summary>出一次招花多少咒力（名义值，实际要乘咒力池的消耗倍率）。一次出招只付一次。</summary>
    public float Cost = 800f;

    /// <summary>两刀之间隔多久（秒）。0 = 同时放出去。</summary>
    public float Gap = 0.15f;

    /// <summary>斩击飞多快（像素/秒）。</summary>
    public float Speed = 1400f;

    /// <summary>飞多久还没砍到人就消失（秒）。</summary>
    public float Life = 1.6f;

    /// <summary>斩痕多长（像素）。</summary>
    public float Size = 110f;

    /// <summary>一刀命中刻几格。</summary>
    public int Marks = 1;

    /// <summary>要不要打提前量（朝对手"将要去的地方"砍）。关掉就是纯直线瞄准。</summary>
    public bool Lead = true;

    /// <summary>招式表里这一招叫什么。</summary>
    public string Move = "dismantle";

    /// <summary>斩痕的颜色（白里带一点冷蓝）。</summary>
    public Color SlashColor = new Color(0.93f, 0.96f, 1f, 0.95f);

    private Ball _ball;
    private CursedEnergy _pool;
    private int _left;      // 还有几刀没放
    private float _gapLeft; // 下一刀还要等多久

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Hits = JsonTool.GetValue(parameters, "hits", Hits);
        Damage = JsonTool.GetValue(parameters, "damage", Damage);
        Cost = JsonTool.GetValue(parameters, "cost", Cost);
        Gap = JsonTool.GetValue(parameters, "gap", Gap);
        Speed = JsonTool.GetValue(parameters, "speed", Speed);
        Life = JsonTool.GetValue(parameters, "life", Life);
        Size = JsonTool.GetValue(parameters, "size", Size);
        Marks = JsonTool.GetValue(parameters, "marks", Marks);
        Lead = JsonTool.GetValue(parameters, "lead", Lead);
        Move = JsonTool.GetValue(parameters, "move", Move);
    }

    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，斩不出去。");
            return;
        }

        _pool = Find<CursedEnergy>(ball, CursedEnergy.TypeName);
        if (_pool == null)
        {
            GD.PushError($"[{Type}] 球上没有 {CursedEnergy.TypeName}，斩不出去。");
            return;
        }

        // 认领招式：出招器挑中这一招，我就出刀
        ball.Events.Register(EventName.attack_started, new EventResponseFunction
        {
            priority = 0,
            action = OnAttackStarted,
        });
    }

    private bool OnAttackStarted(object arg)
    {
        if (_ball == null || arg is not string move || move != Move || _left > 0)
        {
            return true; // 通知类事件：一律放行
        }

        if (!_pool.TrySpend(Cost))
        {
            GD.Print($"[{Type}] {_ball.Name} 咒力不够，斩不出去");
            return true;
        }

        _left = Mathf.Max(Hits, 1);
        _gapLeft = 0f;
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_left <= 0 || _ball == null)
        {
            return;
        }

        if (_ball.State == BallState.Dead)
        {
            _left = 0; // 人没了就不接着砍了
            return;
        }

        _gapLeft -= (float)delta;
        if (_gapLeft > 0f)
        {
            return;
        }

        Slash();
        _left--;
        _gapLeft = Mathf.Max(Gap, 0f);
    }

    /// <summary>放一刀：朝最近的敌人飞过去（每一刀都重新看一眼谁最近）。</summary>
    private void Slash()
    {
        var parent = _ball.GetParent();
        var target = DamageProjectile.NearestEnemyOf(_ball, _ball.Group, _ball.GlobalPosition);
        if (parent == null || target == null)
        {
            GD.Print($"[{Type}] {_ball.Name} 找不到对手，这一刀空放");
            return;
        }

        var scene = GD.Load<PackedScene>(SlashScene);
        if (scene == null)
        {
            GD.PushError($"[{Type}] 找不到斩击的预制体：{SlashScene}");
            return;
        }

        // 朝"对手将要去的地方"打：直着打多半会落空（球在跑）
        var direction = Lead
            ? DamageProjectile.LeadDirection(_ball.GlobalPosition, target, Speed)
            : target.GlobalPosition - _ball.GlobalPosition;

        var slash = scene.Instantiate<SlashProjectile>();
        slash.Name = DamageProjectile.Numbered("斩击·解");
        slash.Position = _ball.Position;
        slash.Launch(_ball, Damage, Type, direction, Speed, Life, Size, SlashColor, Marks);
        parent.AddChild(slash);

        GD.Print($"[{Type}] {_ball.Name} 放出斩击（{Damage:0} 伤害，朝 {target.Name}）");
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
