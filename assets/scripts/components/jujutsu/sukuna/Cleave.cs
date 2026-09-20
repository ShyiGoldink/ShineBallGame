using Godot;

/// <summary>
/// **捌**（`2008`）：宿儺的那把"看人下菜"的斩——**对面越硬，砍得越狠**。
/// 伤害 = `base + 对手最大血量 × ratio`（默认 `500 + 8%`：对 12000 血的五条是 1460），
/// 所以它是把"血厚的对手"拉回同一水平的那一刀（原作里捌就是按对象的强度/咒力量自动调整的）。
///
/// 强度按什么算**是这个文件的参数**：现在读的是对手的**最大血量**（`ratio`）。
/// 想改成"按对方护盾""按对方攻击力"也行——那是 `Measure()` 里一行的事，
/// 因为这一刀只跟"目标"有关，跟伤害链、跟别的组件都不认识。
///
/// 放出去的还是飞行斩击（`SlashProjectile`），命中之后自己刻一格（记在 `7003` 上）。
/// </summary>
public partial class Cleave : BallComponent
{
    private const string SlashScene = "res://assets/scene/projectiles/Slash.tscn";

    public override int Id => 2008;

    public override string Type => "attack.cleave";

    public override string DisplayName => "捌";

    public override string Description => "按对手强度调整伤害的斩击（默认读对手最大血量），命中刻一格刻印";

    /// <summary>前置组件：出招花咒力（池子），命中记刻印（刻印池）。</summary>
    public override string[] Requirements => new[] { CursedEnergy.TypeName, SlashMark.TypeName };

    /// <summary>基础伤害（不管对手多弱，至少这么多）。</summary>
    public float Base = 500f;

    /// <summary>按对手最大血量的百分之多少加伤（0.08 = 8%）。</summary>
    public float Ratio = 0.08f;

    /// <summary>这一刀的伤害上限。0 = 不封顶（对面血再厚也不会一刀砍穿）。</summary>
    public float MaxDamage;

    /// <summary>出一次招花多少咒力（名义值）。</summary>
    public float Cost = 1200f;

    /// <summary>斩击飞多快（像素/秒）。</summary>
    public float Speed = 1300f;

    /// <summary>飞多久还没砍到人就消失（秒）。</summary>
    public float Life = 1.8f;

    /// <summary>斩痕多长（像素）。比解的那一刀大一点——毕竟砍的是"更强的家伙"。</summary>
    public float Size = 140f;

    /// <summary>一刀命中刻几格。</summary>
    public int Marks = 1;

    /// <summary>要不要打提前量（朝对手"将要去的地方"砍）。关掉就是纯直线瞄准。</summary>
    public bool Lead = true;

    /// <summary>招式表里这一招叫什么。</summary>
    public string Move = "cleave";

    /// <summary>斩痕的颜色（白里带一点血红，跟"解"那一刀分得开）。</summary>
    public Color SlashColor = new Color(1f, 0.86f, 0.84f, 0.95f);

    private Ball _ball;
    private CursedEnergy _pool;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Base = JsonTool.GetValue(parameters, "base", Base);
        Ratio = JsonTool.GetValue(parameters, "ratio", Ratio);
        MaxDamage = JsonTool.GetValue(parameters, "max_damage", MaxDamage);
        Cost = JsonTool.GetValue(parameters, "cost", Cost);
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

        ball.Events.Register(EventName.attack_started, new EventResponseFunction
        {
            priority = 0,
            action = OnAttackStarted,
        });
    }

    private bool OnAttackStarted(object arg)
    {
        if (_ball == null || arg is not string move || move != Move)
        {
            return true;
        }

        var target = DamageProjectile.NearestEnemyOf(_ball, _ball.Group, _ball.GlobalPosition);
        if (target == null)
        {
            GD.Print($"[{Type}] {_ball.Name} 找不到对手，这一刀空放");
            return true;
        }

        if (!_pool.TrySpend(Cost))
        {
            GD.Print($"[{Type}] {_ball.Name} 咒力不够，斩不出去");
            return true;
        }

        float damage = DamageAgainst(target);

        var parent = _ball.GetParent();
        var scene = parent == null ? null : GD.Load<PackedScene>(SlashScene);
        if (scene == null)
        {
            GD.PushError($"[{Type}] 球不在场景里、或者找不到斩击的预制体：{SlashScene}");
            return true;
        }

        var direction = Lead
            ? DamageProjectile.LeadDirection(_ball.GlobalPosition, target, Speed)
            : target.GlobalPosition - _ball.GlobalPosition;

        var slash = scene.Instantiate<SlashProjectile>();
        slash.Name = DamageProjectile.Numbered("斩击·捌");
        slash.Position = _ball.Position;
        slash.Launch(_ball, damage, Type, direction, Speed, Life, Size, SlashColor, Marks);
        parent.AddChild(slash);

        GD.Print($"[{Type}] {_ball.Name} 放出捌：对手最大血量 {target.MaxHp:0} → 这一刀 {damage:0}");
        return true;
    }

    /// <summary>
    /// 这一刀打这个目标多少：`base + 目标最大血量 × ratio`（`max_damage` 配了的话再夹一下）。
    /// **"越强"怎么定义就是这一行**——想改成读护盾、读攻击力，改这里。
    /// </summary>
    private float DamageAgainst(Ball target)
    {
        float damage = Base + target.MaxHp * Ratio;
        return MaxDamage > 0f ? Mathf.Min(damage, MaxDamage) : damage;
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
