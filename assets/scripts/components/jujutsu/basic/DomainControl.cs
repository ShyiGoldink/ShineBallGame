using Godot;

/// <summary>
/// 领域压制（`8002`）：**"被对方的领域罩住"这件事的载体**，挂在**挨罩的那颗球**身上
/// （攻击方用自己的 `enemycomponents` 挂过去，和苍/赫牵引 `1002` 是同一种交付方式）。
///
/// 所以"领域的效果是什么"和"领域画成什么样"是分开的：`Domain`（`8xxx`）只管场上那个圈，
/// 这里管"站在圈里会怎样"。同一个领域想换效果，换这个组件就行；这个组件也能被别的领域复用。
///
/// 规则（每帧现算，不需要谁去通知它）：
/// * **自己有生效中的领域 → 不发作**：领域对领域是"先由各自的领域顶着"，
///   所以两边都开着领域的时候，谁都还没被对方的领域吃住（见 `Domain` 的优先级那一段）；
/// * **站在别人的领域里、自己又没有领域 → 被压住**：进入受控状态，时长 `duration`；
/// * `duration` 默认 100000 秒 = **这一局就废了**（无量空处就是配成这样：落地即定胜负）；
/// * `release_when_free` 是给别的领域留的口子：配 true 的话，罩住你的那个领域一消失
///   （时间到 / 被打碎 / 被覆盖），你就被放开。无量空处配 false —— 中了就是中了。
///
/// **"不能动"是怎么落实的**：受控状态本身只是"谁被接管了"，接管之后怎么动由控制类组件决定；
/// 一颗球要是还挂着减速（`6001`），减速会接手按 30% 走，那就不叫定身了。
/// 所以这个组件自己也是个控制类组件（类别 `stun`、类别优先级 200，压过减速的 100），
/// 由仲裁（`BallControl.PickDriver`）把方向盘判给它，它把速度清零——球就真的不动了。
/// </summary>
public partial class DomainControl : BallComponent
{
    public override int Id => 8002;

    public override string Type => "domain.control";

    public override string DisplayName => "领域压制";

    public override string Description => "站在敌方生效中的领域里、自己又没有领域时被定住；领域消失后放不放开由 release_when_free 配";

    /// <summary>被压住多久（秒）。默认 100000 = 这一局基本就废了。</summary>
    public float Duration = 100000f;

    /// <summary>罩住你的领域消失了，要不要放开。无量空处那种"落地即定胜负"的配 false。</summary>
    public bool ReleaseWhenFree;

    private Ball _ball;
    private bool _caught;

    /// <summary>控制类别：`stun`（定身）。和减速（`slow`）比，类别优先级更高。见 `BallControl`。</summary>
    public override string ControlCategory => "stun";

    public override int ControlCategoryPriority => 200;

    public override int ControlStrength => 100;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Duration = JsonTool.GetValue(parameters, "duration", Duration);
        ReleaseWhenFree = JsonTool.GetValue(parameters, "release_when_free", ReleaseWhenFree);
    }

    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，领域压不住人。");
            return;
        }

        GD.Print($"[{Type}] {ball.Name} 会被敌方领域压住（{Duration:0}s"
            + (ReleaseWhenFree ? "，领域没了就放开）" : "，中了就锁死）"));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_ball == null || _ball.State == BallState.Dead)
        {
            return;
        }

        bool wasCaught = _caught;

        if (!Caught())
        {
            // 已经中了、而且配的是"锁死"：就不再回头看了（无量空处就是这种）
            if (_caught && ReleaseWhenFree)
            {
                _caught = false;
                _ball.EndControl();
                GD.Print($"[{Type}] {_ball.Name} 从领域压制里放出来了");
            }

            return;
        }

        _caught = true;

        if (!wasCaught)
        {
            GD.Print($"[{Type}] {_ball.Name} 站在敌方领域里、自己又没有领域，被压住了（{Duration:0}s）");
        }

        // 受控状态只在"移动中"才切得进去，所以每帧再试一次：
        // 对方要是在出招/登场，就等它落地的那一帧再压住
        if (_ball.State == BallState.Move)
        {
            _ball.BeControlled(Duration, ControlRepeat.Ignore);
        }

        // 定身：只有仲裁把方向盘判给我时才动手（不然就把减速那套抢了）
        if (_ball.State == BallState.Controlled && BallControl.PickDriver(_ball) == this)
        {
            _ball.Velocity = Vector2.Zero;
        }
    }

    /// <summary>
    /// 这会儿是不是被压住了：**站在敌方生效中的领域里，而且自己身上没有生效中的领域**。
    /// </summary>
    private bool Caught()
    {
        if (DomainQuery.Contesting(_ball))
        {
            return false; // 自己也有领域（或者正在开）：先由领域顶着，还没轮到自己吃效果
        }

        return DomainQuery.CoveringField(_ball) != null;
    }
}
