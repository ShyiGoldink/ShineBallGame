using Godot;

/// <summary>
/// 受控禁手（`6002`）：**自己处在受控状态时，把自己的命中圈关掉**——不能靠碰撞还手。
///
/// 为什么需要它：撞击型的攻击（`2001 attack.collision`、`2004 attack.basic`）是**不看状态的**
/// ——它们订的是自己的命中圈（`HitArea`）被碰到的事件，所以球被定住了照样能撞人。
/// 这一条把"被控住就还不了手"补上：受控一进就把命中圈关掉，出控再打开。
/// 于是"定身"才是真的定住，而不是"站着不动但一身是刺"。
///
/// 它是**通用件**，不特判任何领域：只要是受控状态就生效，来源可以是
/// 领域压制（`8002`）、屎蛋、以后的眩晕……想让谁守这条规矩，给他装上就行。
/// 无量空处的受害者是靠五条悟的 `enemycomponents` 把这一条一起送过去的。
///
/// 关的是 `HitArea.Monitoring`（**自己**感知别人），不碰 `CollisionShape2D`：
/// 球该被撞还是会被撞，只是它自己不再主动打人。
/// </summary>
public partial class MuteAttack : BallComponent
{
    public override int Id => 6002;

    public override string Type => "control.mute_attack";

    public override string DisplayName => "受控禁手";

    public override string Description => "受控状态下关掉自己的命中圈：不能靠碰撞攻击还手（出控自动打开）";

    private Ball _ball;
    private Area2D _hitArea;

    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，禁手接不上。");
            return;
        }

        _hitArea = ball.GetNodeOrNull<Area2D>("HitArea");
        if (_hitArea == null)
        {
            GD.PushError($"[装配] 预制体里找不到 HitArea，这次禁手接不上。");
            return;
        }

        ball.Events.Register(EventName.state_changed, new EventResponseFunction
        {
            priority = 0,
            action = OnStateChanged,
        });

        Apply(ball.State); // 挂上来的时候可能已经受控了
    }

    /// <summary>进场景树之后再落一次：`Bind` 那会儿球还没进树，`Monitoring` 的改动可能被引擎盖掉。</summary>
    public override void _Ready()
    {
        if (_ball != null)
        {
            Apply(_ball.State);
        }
    }

    private bool OnStateChanged(object arg)
    {
        if (arg is BallState state)
        {
            Apply(state);
        }

        return true; // 通知类事件：一律放行，别把后面组件的收听堵掉
    }

    /// <summary>受控就闭嘴，其它状态照常打人。</summary>
    private void Apply(BallState state)
    {
        if (_hitArea == null)
        {
            return;
        }

        bool mute = state == BallState.Controlled;
        bool monitoring = !mute;

        if (_hitArea.Monitoring != monitoring)
        {
            _hitArea.Monitoring = monitoring; // 没变就不写（写一次会重新派发一遍进出事件）
        }
    }
}
