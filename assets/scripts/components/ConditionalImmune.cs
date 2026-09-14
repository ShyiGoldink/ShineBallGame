using Godot;
using System;

/// <summary>
/// 条件性无敌：**在指定状态下阻塞整条受伤链**（返回 false），于是这个状态下打不疼它。
///
/// 默认条件是"攻击状态"——`pulipuli` 用它达成"攻击期间无敌"。
/// 优先级默认取 `DamagePriority.AbsoluteImmune`（1000，最高），所以排在护盾、中毒、普通扣血前面；
/// 条件不满足时返回 true，放行给后面的处理函数。换句话说它只堵"对不对"、不改伤害数值。
/// </summary>
public partial class ConditionalImmune : BallComponent
{
    public override int Id => 3001;

    public override string Type => "defense.conditional_immune";

    public override string Description => "指定状态下阻塞受伤链，实现条件无敌";

    /// <summary>受伤链上的优先级，默认最高（1000）。</summary>
    public int Priority = DamagePriority.AbsoluteImmune;

    /// <summary>哪个状态下无敌：写状态名（不区分大小写），默认 attack（攻击）。</summary>
    public string State = "attack";

    private Ball _ball;
    private BallState _immuneState = BallState.Attack;
    private bool _ready;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Priority = JsonTool.GetValue(parameters, "priority", Priority);
        State = JsonTool.GetValue(parameters, "state", State);
    }

    /// <summary>装配时接线：解析"哪个状态无敌"，然后按可配的优先级挂到受伤链上。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，条件无敌不生效。");
            return;
        }

        if (!Enum.TryParse(State, true, out _immuneState))
        {
            GD.PushWarning($"[{Type}] 不认识的状态名 '{State}'，按默认的攻击状态处理。");
            _immuneState = BallState.Attack;
        }

        _ready = true;

        ball.Events.Register(EventName.take_damage, new EventResponseFunction
        {
            priority = Priority,
            action = OnTakeDamage,
        });
    }

    /// <summary>受伤链上的处理：正好是"要无敌"的状态就挡下来（false），其它状态放行（true）。</summary>
    public bool OnTakeDamage(object arg)
    {
        if (!_ready || _ball == null)
        {
            return true; // 没准备好就放行，别把伤害链堵死
        }

        if (_ball.State != _immuneState)
        {
            return true; // 不在要无敌的状态里：放行
        }

        GD.Print($"[{Type}] {_ball.Name} 正在{_immuneState}，这次伤害挡下");
        return false; // 阻塞后面的处理函数 = 这个状态下无敌
    }
}
