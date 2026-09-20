using Godot;

/// <summary>
/// 中毒：受伤链里夹在护盾（700）和一般扣血（500）中间的那一环，优先级 600。
///
/// 它**不阻塞**（返回 true），只干一件事：把球染成紫色，过一小会儿褪回去。
/// 也就是说它不改伤害，只是"挨打的时候紫一下"——毒伤本身是攻击方每秒打过来的，
/// 这里只是把"中毒了"这件事画出来。
///
/// 想看到紫色，被打的那颗球身上就得挂这个组件（写它自己的 selfcomponents 里）。
/// </summary>
public partial class Poison : BallComponent
{
    public override int Id => 4002;

    public override string Type => "behavior.poison";

    public override string Description => "受伤链里排在护盾后面、一般扣血前面：不阻塞，把球染紫一下（中毒的表现）";

    /// <summary>染紫持续多久（秒）。</summary>
    public float TintTime = 0.2f;

    private Ball _ball;
    private float _tintLeft;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        TintTime = JsonTool.GetValue(parameters, "tint_time", TintTime);
    }

    /// <summary>装配时接线：拿到球、把自己注册进受伤链（优先级 600，夹在护盾和普通扣血中间）。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，染不了色。");
            return;
        }

        ball.Events.Register(EventName.take_damage, new EventResponseFunction
        {
            priority = DamagePriority.DamageOverTime,
            action = OnTakeDamage,
        });
    }

    /// <summary>受伤链上的处理：染色 + 放行（不阻塞，后面的护盾/扣血照常走）。</summary>
    public bool OnTakeDamage(object arg)
    {
        _tintLeft = TintTime;

        if (_ball != null)
        {
            _ball.Modulate = PoisonColor;
        }

        return true;
    }

    public override void _Process(double delta)
    {
        if (_tintLeft <= 0f)
        {
            return;
        }

        _tintLeft -= (float)delta;
        if (_tintLeft > 0f)
        {
            return;
        }

        if (_ball != null)
        {
            _ball.Modulate = Colors.White;
        }
    }

    /// <summary>中毒色。用球的 Modulate，所以本体、血条、以后挂上去的 Spine 会一起变色。</summary>
    private static readonly Color PoisonColor = new Color(0.72f, 0.45f, 1f);
}
