using Godot;

/// <summary>
/// 沙包受伤（`4006`）：**扣血，但至少留一口气**——血量永远不会掉到 `min_hp` 以下，
/// 所以挂了它的球打不死。沙包就是干这个的：拿来试伤害、试连招、量数值。
///
/// ⚠️ 它是 `4001 behavior.normal_damage` 的**替代品，不是搭档**：两者都是"受伤链的最后一环"
/// （同一个优先级、都返回 false 把后面截断），**一颗球上只能挂一个**，
/// 两个都挂会各扣一次血。
///
/// 和 `4001` 一样，它按 `DamageEvent.Amount` 扣血（也就是前面几环——无敌、毒血、护盾——
/// 处理过之后的那个值），并在最后打一行 `[伤害]` 日志，格式和 `4001` 一致，只是
/// **记的是实际掉了多少**、到底了还会补一句"留了多少"。
///
/// 唯一的漏洞是 `4004 behavior.poison_damage`（毒血）：它为了让毒绕过护盾，
/// 是**自己直接扣血**的，绕开了这条链，所以能把这层保底打穿。
/// </summary>
public partial class SandbagDamage : BallComponent
{
    public override int Id => 4006;

    public override string Type => "behavior.sandbag_damage";

    public override string DisplayName => "沙包受伤";

    public override string Description => "受伤链的最后一环：扣血，但血量不会掉到 min_hp 以下（默认留 1 点）";

    /// <summary>扣完至少留多少血。默认 1：永远差一口气，死不了。</summary>
    public float MinHp = 1f;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        MinHp = JsonTool.GetValue(parameters, "min_hp", MinHp);
    }

    /// <summary>装配时接线：把自己挂到受伤链的最后一环（优先级 500，和 `4001` 同一格）。</summary>
    public override void Bind(Ball ball, string configId)
    {
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，扣不了血。");
            return;
        }

        ball.Events.Register(EventName.take_damage, new EventResponseFunction
        {
            priority = DamagePriority.NormalDamage,
            action = OnTakeDamage,
        });
    }

    /// <summary>
    /// 受伤链的最后一环：扣血、兜住下限，然后返回 false 截断。
    /// 扣完不低于 `min_hp`，所以血量永远不会 <= 0，也就永远不会进死亡状态。
    /// </summary>
    public bool OnTakeDamage(object arg)
    {
        if (GetParent() is not Ball ball || arg is not DamageEvent hit)
        {
            GD.PushError($"[{Type}] 扣血失败：组件没挂在球下面，或者收到的不是伤害事件。");
            return true; // 出问题就放行，别把后面的处理链堵死
        }

        float floor = Mathf.Max(MinHp, 0f);
        float after = Mathf.Max(floor, ball.Hp - hit.Amount);
        float lost = ball.Hp - after; // 实际掉了多少（到底了就比 hit.Amount 小）

        string from = hit.Source != null
            ? $"{hit.Source.Name} 的 {hit.SourceType ?? "?"}"
            : hit.SourceId != null ? $"{hit.SourceId} 的 {hit.SourceType ?? "?"}" : "未知来源";

        GD.Print($"[伤害] {ball.Name} -{lost:0.#}（{from}）"
            + (lost < hit.Amount ? $"：到底了，留 {floor:0.#} 点" : string.Empty));

        ball.Hp = after;
        return false;
    }
}
