using Godot;

/// <summary>
/// **伏魔御厨子的必中斩**（`8004`）：挂在**被罩住的那颗球**身上（宿儺用自己的 `enemycomponents`
/// 送过去，和苍/赫牵引、领域压制是同一种交付方式），只要站在伏魔御厨子里就**每 `interval` 秒挨一次斩**。
///
/// 三条规矩：
/// 1. **必中先打领域**：挨打的人要是自己有生效中的领域，这一下**先算在它的领域上**
///    （`Domain.Absorb`）——非开放型拿去磨外壳、开放型直接抵消。领域挡下来了，人就不吃这一下。
///    这也正是"领域能互相抵消必中"。
/// 2. 领域被覆盖（优先级差 10 以上）或者碎了之后，人就**直接吃**这一下——所以"谁的领域先完，谁先暴露"。
/// 3. 不管挡没挡住，**这一斩都算在领域持有者头上**：给它的斩击刻印（`7003`）加一格。
///    这就是"领域展开 → 灶開"那条连招——圈里的人越待越久，宿儺手里攒的刻印越多。
///
/// 伤害走**正常受伤链**（护盾、无敌、无下限照常算），所以"回血跟得上就扛得住"是对的：
/// 这一下每秒 1200，五条的反转术式 + 无下限够快就压得住。
/// </summary>
public partial class MalevolentSlash : BallComponent
{
    public override int Id => 8004;

    public override string Type => "domain.malevolent_shrine_slash";

    public override string DisplayName => "御厨子的斩击";

    public override string Description => "站在伏魔御厨子里每 interval 秒挨一次必中斩；必中先打在保护自己的领域上";

    /// <summary>隔几秒挨一下。</summary>
    public float Interval = 1f;

    /// <summary>每一下的伤害。</summary>
    public float Damage = 1200f;

    /// <summary>这一斩给领域持有者刻几格。</summary>
    public int Marks = 1;

    private Ball _ball;
    private float _left;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Interval = JsonTool.GetValue(parameters, "interval", Interval);
        Damage = JsonTool.GetValue(parameters, "damage", Damage);
        Marks = JsonTool.GetValue(parameters, "marks", Marks);
    }

    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，斩不动。");
            return;
        }

        _left = Mathf.Max(Interval, 0.01f);

        GD.Print($"[{Type}] {ball.Name} 会被伏魔御厨子每秒斩一下（{Damage:0} 伤害）");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_ball == null || _ball.State == BallState.Dead)
        {
            return;
        }

        var field = DomainQuery.CoveringField(_ball);
        if (field == null)
        {
            _left = Mathf.Max(Interval, 0.01f); // 不在别人领域里：计时重新开始
            return;
        }

        _left -= (float)delta;
        if (_left > 0f)
        {
            return;
        }

        _left = Mathf.Max(Interval, 0.01f);
        SlashOnce(DomainQuery.OwnerOf(field));
    }

    /// <summary>斩一下：先打在保护自己的领域上，没挡住才落到人身上；无论哪种都记在持有者头上。</summary>
    private void SlashOnce(Ball owner)
    {
        var own = DomainQuery.OwnDomain(_ball);
        bool absorbed = own != null && own.Absorb(Damage);

        if (absorbed)
        {
            GD.Print($"[{Type}] {_ball.Name} 的领域挡下了必中斩（{Damage:0}），这一下没进人身上");
        }
        else
        {
            _ball.TakeDamage(new DamageEvent(_ball, Damage, Type, null, owner?.Id));
        }

        var mark = SlashMark.Find(owner);
        mark?.Add(Marks);
    }
}
