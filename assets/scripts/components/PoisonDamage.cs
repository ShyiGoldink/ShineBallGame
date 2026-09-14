using Godot;

/// <summary>
/// 毒血：**让"毒"这种伤害绕过护盾，直接进血**。
///
/// 它挂在**被打的那颗球**身上（攻击方写在自己的 `enemycomponents` 里，装配时会挂到对面），
/// 占的是受伤链的 800（`DamagePriority.TrueDamage`）那一格——比护盾（700）先跑，
/// 所以护盾还没来得及吃，伤害就已经进血了。
///
/// 干活方式：认出来是毒，就自己从血量里扣掉，然后**把剩下的伤害抹成 0** 再往下传。
/// 后面几环看到的就是"这一下没有伤害"——护盾不会吸收（`3002` 见 `Amount <= 0` 直接放行），
/// `4001` 照常打一行 `-0`。**不阻塞**（返回 true），所以中毒染色（600）还会照常亮一下。
///
/// 参数：`source_prefix`（默认 `"egg."`）——哪种伤害算毒。按伤害事件里的 `SourceType`
/// 做前缀匹配：蛋类造成的伤害都是 `egg.xxx`（屎蛋是 `egg.shit`）。
/// </summary>
public partial class PoisonDamage : BallComponent
{
    public override int Id => 4004;

    public override string Type => "behavior.poison_damage";

    public override string DisplayName => "毒血";

    public override string Description => "毒（蛋造成的伤害）绕过护盾、直接扣血";

    /// <summary>哪种伤害算毒：按来源的类型名前缀匹配。默认所有蛋的伤害。</summary>
    public string SourcePrefix = "egg.";

    private Ball _ball;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        SourcePrefix = JsonTool.GetValue(parameters, "source_prefix", SourcePrefix);
    }

    /// <summary>装配时接线：挂到受伤链的真伤那一格（800，比护盾的 700 先跑）。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，毒伤不会绕过护盾。");
            return;
        }

        ball.Events.Register(EventName.take_damage, new EventResponseFunction
        {
            priority = DamagePriority.TrueDamage,
            action = OnTakeDamage,
        });
    }

    /// <summary>受伤链上的处理：是毒就自己扣血、把剩下的抹成 0；不是毒就原样放行。</summary>
    public bool OnTakeDamage(object arg)
    {
        if (_ball == null || arg is not DamageEvent hit || hit.Amount <= 0f)
        {
            return true; // 没伤害要处理
        }

        if (string.IsNullOrEmpty(SourcePrefix)
            || hit.SourceType == null
            || !hit.SourceType.StartsWith(SourcePrefix))
        {
            return true; // 不是毒：放行，护盾和 4001 按平常的规矩来
        }

        float damage = hit.Amount;
        hit.Amount = 0f; // 护盾看到 0 就不会吸收；4001 会照常打一行 -0

        GD.Print($"[{Type}] {_ball.Name} 中毒 -{damage:0.#}（绕过护盾）");
        _ball.Hp -= damage;
        return true;
    }
}
