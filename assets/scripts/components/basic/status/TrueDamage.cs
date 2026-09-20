using Godot;

/// <summary>
/// 真伤（`4008`）：**这一类伤害绕过护盾，直接进血**。
///
/// 谁需要它：**领域里的必中**。领域里的斩击是"已经命中"的，护盾那套（无下限每秒灌盾）
/// 挡不住它——原作里五条在伏魔御厨子里也只能靠反转术式硬扛，就是这个道理。
/// 所以宿儺在自己的 `enemycomponents` 里给对面挂上它、`source_prefix` 配 `"domain."`，
/// 于是"领域造成的伤害"绕过护盾，"平A/斩击造成的伤害"照常被护盾吃。**要不要真伤是数据说了算**，
/// 领域本体（`8001`/`8003`）和效果（`8004`）都不认识这个组件。
///
/// 干活方式和 `4004` 毒血一模一样：挂在**被打的那颗球**身上，占受伤链的 800 那一格
/// （比领域外壳 750、护盾 700 都先跑），认出是自己的种类就**自己从血量里扣**、
/// 再把 `Amount` 抹成 0 让后面几环无事可做。**不阻塞**（返回 true），所以染色之类照旧。
///
/// 它和毒血是同一格、同一套路子；区别只在"认哪种来源"：
/// 毒血认 `egg.`（蛋），真伤认 `domain.`（领域）。
/// ⚠️ 一颗球上两个都挂不冲突（认的前缀不同），但**同一格上挂两个认同一个前缀的就会各扣一次**。
/// </summary>
public partial class TrueDamage : BallComponent
{
    public override int Id => 4008;

    public override string Type => "behavior.true_damage";

    public override string DisplayName => "真伤";

    public override string Description => "这一类来源的伤害（默认领域造成的）绕过护盾、直接扣血";

    /// <summary>哪种伤害算真伤：按来源的类型名前缀匹配。默认所有领域造成的伤害。</summary>
    public string SourcePrefix = "domain.";

    private Ball _ball;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        SourcePrefix = JsonTool.GetValue(parameters, "source_prefix", SourcePrefix);
    }

    /// <summary>装配时接线：挂到受伤链的真伤那一格（800，比外壳 750、护盾 700 都先跑）。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，真伤不会绕过护盾。");
            return;
        }

        ball.Events.Register(EventName.take_damage, new EventResponseFunction
        {
            priority = DamagePriority.TrueDamage,
            action = OnTakeDamage,
        });
    }

    /// <summary>受伤链上的处理：是这一类就自己扣血、把剩下的抹成 0；不是就原样放行。</summary>
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
            return true; // 不是这一类的伤害：放行，护盾和 4001 按平常的规矩来
        }

        float damage = hit.Amount;
        hit.Amount = 0f; // 护盾看到 0 就不会吸收；4001 会照常打一行 -0

        GD.Print($"[{Type}] {_ball.Name} 吃真伤 -{damage:0.#}（{hit.SourceType}，绕过护盾）");
        _ball.Hp -= damage;
        return true;
    }
}
