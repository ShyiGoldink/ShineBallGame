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

    /// <summary>
    /// 打到敌人时播的音效，写文件名或路径（怎么找见 SoundTool）。留空就不播。
    /// 只在"这一下真打出去了"的时候响：撞到自己、自己人、或者自己已经死了都不响。
    /// </summary>
    public string Sound = string.Empty;

    /// <summary>音效的播放音高：1 = 原样，小于 1 更低沉（盾球的撞击声用 0.7 压闷一点）。</summary>
    public float Pitch = 1f;

    ///<summary>group，代表小球的来自哪里，由工厂填</summary>///
    public int Group = 0; 

    private Ball _ball;
    private bool _alive = true;

    /// <summary>哪颗球的 Json 配出了我（从 enemycomponents 送人时，是**送出去的那颗球**）。</summary>
    private string _configId;

    /// <summary>
    /// 装配时接线：阵营跟着**挂我的那颗球**走；把自己的处理函数接到球的检测圈上，
    /// 状态变化也在这里订——死了就不再打人。
    /// </summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        _configId = configId;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，没法判断生死。");
            return;
        }

        Group = ball.Group;

        var hitArea = ball.GetNodeOrNull<Area2D>("HitArea");
        if (hitArea == null)
        {
            GD.PushError("[装配] 预制体里找不到 HitArea，这次碰撞攻击接不上。");
        }
        else
        {
            hitArea.BodyEntered += OnBodyEntered;
        }

        // 状态一变球就通知我，死了就不再打人
        _ball.Events.Register(EventName.state_changed, new EventResponseFunction { priority = 0, action = OnStateChanged });

        // 先按当前状态对一次表（球挂上组件之前可能已经切过状态了，那次通知收不到）
        _alive = _ball.State != BallState.Dead;
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
        Sound = JsonTool.GetValue(parameters, "sound", Sound);
        Pitch = JsonTool.GetValue(parameters, "pitch", Pitch);
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
        // 伤害带上来源：谁打的（自己）+ 哪种攻击
        enemy.TakeDamage(new DamageEvent(enemy, Damage, Type, _ball));

        // 音效跟着"这一下打出去"走：放到扣血之后，前面那些不算数的碰撞都不会响
        // 音效按"配我这个组件的那颗球"的包去找——攻击可以当礼物送给对面，
        // 那种情况下音效还是应该在送礼物那颗球自己的 resource 里
        SoundTool.PlayOnce(this, Sound, _configId ?? _ball?.Id, Pitch);
    }
}
