using Godot;

/// <summary>
/// 下蛋攻击：每隔 cd 秒进入一次攻击状态，前摇走完之后在这个位置产下一颗蛋，
/// 攻击状态再持续 attack_time 秒回到移动状态（这段时间是留给 Spine 动画的）。
///
/// 蛋 = 一颗只挂了 egg_id 那个组件的新球：跟普通球共用预制体、同一个阵营，
/// 所以它会被算进场上、也参与胜负判定。蛋上挂什么组件，它就有什么行为。
/// </summary>
public partial class EggAttack : BallComponent
{
    public override int Id => 2002;

    public override string Type => "attack.egg";

    public override string Description => "每隔 cd 秒进入攻击状态，前摇结束后产下一颗蛋";

    /// <summary>两次攻击的间隔（秒），从上次进入攻击状态算起。</summary>
    public float Cd = 5f;

    /// <summary>前摇（秒）：进入攻击状态后过这么久产蛋。</summary>
    public float Windup = 1f;

    /// <summary>攻击状态持续多久（秒）。有 Spine 动画就按动画长度配，没有的话 0.2 就够。</summary>
    public float AttackTime = 0.2f;

    /// <summary>蛋上挂哪个组件（组件编号）。</summary>
    public int EggId;

    private Ball _ball;
    private float _cdLeft;
    private float _windupLeft;
    private bool _laying;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Cd = JsonTool.GetValue(parameters, "cd", Cd);
        Windup = JsonTool.GetValue(parameters, "windup", Windup);
        AttackTime = JsonTool.GetValue(parameters, "attack_time", AttackTime);
        EggId = JsonTool.GetValue(parameters, "egg_id", EggId);
    }

    /// <summary>装配时接线：拿到球、把 CD 设成满的、订"状态变化"（用来知道这次攻击有没有被打断）。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，攻击起不来。");
            return;
        }

        _cdLeft = Cd;

        // 状态一变就知道这次攻击有没有被打断（死了、被拉去干别的），打断了就不产蛋
        _ball.Events.Register(EventName.state_changed, new EventResponseFunction { priority = 0, action = OnStateChanged });
    }

    private bool OnStateChanged(object arg)
    {
        // 只有死亡会打断这次产蛋。
        // 注意别把"回到移动状态"也当成打断：球的攻击计时和这里的前摇是同时开始的，
        // 而父节点（球）先跑——同一帧里球会先切回移动状态，那次通知要是把 _laying 关掉，
        // 前摇就永远走不完，蛋永远产不出来。
        if (arg is BallState state && state == BallState.Dead)
        {
            _laying = false;
        }

        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_ball == null || _ball.State == BallState.Dead)
        {
            return;
        }

        // cd 一直在走：从上次进入攻击状态算起，"每 cd 秒进入一次攻击状态"就是字面意思，
        // 前摇和攻击状态里都不会暂停它。
        _cdLeft -= (float)delta;

        // 前摇：产蛋只发生在攻击状态里
        if (_laying)
        {
            _windupLeft -= (float)delta;
            if (_windupLeft <= 0f)
            {
                _laying = false;
                LayEgg();
            }

            return;
        }

        if (_cdLeft > 0f)
        {
            return;
        }

        // 到点了：能打就打。不能打（登场/受控/正在攻击）就把计时停在 0，
        // 等回到移动状态立刻补上，不丢这一次。
        if (_ball.State != BallState.Move)
        {
            _cdLeft = 0f;
            return;
        }

        _cdLeft = Cd;
        _windupLeft = Windup;
        _laying = true;

        // 攻击状态至少要持续到前摇结束，否则前摇还没走完球就回移动状态了，蛋永远产不出来
        _ball.BeginAttack(Mathf.Max(AttackTime, Windup));
    }

    /// <summary>产蛋：在这个位置放一颗只挂了 EggId 组件的新球，阵营跟自己一样。</summary>
    private void LayEgg()
    {
        if (EggId <= 0)
        {
            GD.PushWarning($"[{Type}] 没配 egg_id，产不出蛋。");
            return;
        }

        var parent = _ball.GetParent();
        if (parent == null)
        {
            GD.PushError($"[{Type}] 球不在场景里，蛋没地方放。");
            return;
        }

        var egg = BallAssembler.BuildEgg(_ball.Position + new Vector2(0f, EggDropDistance()), _ball.Group, EggId, _ball.Id);
        if (egg == null)
        {
            return;
        }

        parent.AddChild(egg);

        GD.Print($"[{Type}] {_ball.Name} 产下一颗蛋（编号 {EggId}）");
    }

    /// <summary>
    /// 蛋落在球的脚下：往下挪的距离跟着**这颗球自己的碰撞圈半径**走
    /// （球多大大就挪多少，所以 100px 的球挪 50、150px 的球挪 75，不用写死数字）。
    /// </summary>
    private float EggDropDistance()
    {
        if (_ball.GetNodeOrNull<CollisionShape2D>("Shape")?.Shape is CircleShape2D circle)
        {
            return circle.Radius;
        }

        return 0f; // 找不到形状就落在球心上，总比乱挪好
    }
}
