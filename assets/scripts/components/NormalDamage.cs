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

    /// <summary>事件处理函数：扣血，返回 false 表示后面的处理函数不再执行。</summary>
    public bool OnTakeDamage(object arg)
    {
        if (GetParent() is not Ball ball || arg is not float damage)
        {
            GD.PushError($"[{Type}] 扣血失败：组件没挂在球下面，或者收到的伤害不是 float。");
            return true; // 出问题就放行，别把后面的处理链堵死
        }

        ball.Hp -= damage;
        return false;
    }
}
