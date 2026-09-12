using Godot;

/// <summary>
/// 碰撞攻击：碰到敌人时，调用对方的 TakeDamage。
/// 处理函数的签名和 Godot 的 body_entered(Node2D body) 一致，
/// 所以装配工厂可以直接把球的碰撞检测接到它上面（比如 Area2D 的 BodyEntered）。
/// </summary>
public partial class CollisionAttack : BallComponent
{
    public override int Id => 2001;

    public override string Type => "attack.collision";

    public override string Description => "碰到敌人时调用对方的 TakeDamage";

    /// <summary>每次碰撞造成的伤害。现在先给个默认值，之后由工厂从 Json 里填。</summary>
    public float Damage = 10f;
    ///<summary>group，代表小球的来自哪里，由工厂填</summary>///
    public int Group = 0; 

    private Ball _ball;
    private bool _alive = true;

    public override void _Ready()
    {
        _ball = GetParent() as Ball;
        if (_ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，没法判断生死。");
            return;
        }

        // 状态一变球就通知我，死了就不再打人
        _ball.Events.Register(EventName.state_changed, new EventResponseFunction { priority = 0, action = OnStateChanged });
    }

    private bool OnStateChanged(object arg)
    {
        // 只有死亡算"不能打人"，登场/移动/受控/攻击都照常
        _alive = arg is not BallState state || state != BallState.Dead;
        return true;
    }

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Damage = JsonTool.GetValue(parameters, "damage", Damage);
    }

    /// <summary>碰撞处理：给对方造成伤害。每次接触只触发一次，离开后再次接触才会再触发。</summary>
    public void OnBodyEntered(Node2D body)
    {
        if (!_alive)
        {
            return; // 已经死了，不能打人
        }

        // 撞到自己不算：球自己的检测区域有可能扫到自己的碰撞盒
        if (body is not Ball enemy || enemy.GetInstanceId() == GetParent().GetInstanceId())
        {
            return;
        }
        if(enemy.Group == this.Group)
        {
            return;
        }
        enemy.TakeDamage(Damage);
    }
}
