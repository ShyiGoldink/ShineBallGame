using Godot;

/// <summary>
/// 一般扣血：受伤链的最后一环，直接把血量扣掉，然后阻塞（返回 false）。
/// 装配工厂按 DamagePriority.NormalDamage 把它注册到 EventName.take_damage 上。
///
/// 组件挂在球下面，所以球就是自己的父节点。等工厂接手绑定之后，
/// 如果改成由工厂把球传进来，这里换个来源就行。
/// </summary>
public partial class NormalDamage : BallComponent
{
    public override int Id => 4001;

    public override string Type => "behavior.normal_damage";

    public override string Description => "受伤链的最后一环：直接扣血，然后阻塞";

    /// <summary>装配时接线：把自己挂到受伤链的最后一环（优先级 500，比护盾、中毒都靠后）。</summary>
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

    /// <summary>受伤链的最后一环：按事件里"还剩多少"扣血，然后返回 false 阻塞后面的处理函数。</summary>
    public bool OnTakeDamage(object arg)
    {
        if (GetParent() is not Ball ball || arg is not DamageEvent hit)
        {
            GD.PushError($"[{Type}] 扣血失败：组件没挂在球下面，或者收到的不是伤害事件。");
            return true; // 出问题就放行，别把后面的处理链堵死
        }

        string from = hit.Source != null
            ? $"{hit.Source.Name} 的 {hit.SourceType ?? "?"}"
            : hit.SourceId != null ? $"{hit.SourceId} 的 {hit.SourceType ?? "?"}" : "未知来源";

        GD.Print($"[伤害] {ball.Name} -{hit.Amount:0.#}（{from}）");
        ball.Hp -= hit.Amount;
        return false;
    }
}
