using Godot;

/// <summary>
/// 苍/赫牵引（`1002 movement.jujutsu_motion`）：**场上有苍球/赫球时，把这一帧的移动接手过来**，
/// 被苍吸过去、被赫推开来；场上没有就返回 true 放行，普通移动照常走。
///
/// 它挂在**被影响的那颗球**身上（五条悟选球时通过 `enemycomponents` 送给对手，
/// 和 `6001` 减速是同一种交付方式），所以被移动的永远是宿主自己——
/// 方向 = 宿主 → 苍球（吸引）、背离（排斥），不涉及"去动别人的球"。
///
/// 走的是移动链（见 `MovePriority`），不是受控状态：
/// * 状态还是移动，所以对手**照常能出招**（受控会把 `BeginAttack` 挡掉，那是控制技不是位移）；
/// * 链只在移动状态发，所以登场、受控（那会儿方向盘归 `BallControl` 那套）、攻击、死亡都不插手；
/// * 处理完返回 **false**，普通移动那一环就被阻塞掉了，**不会出现两个驱动者**。
///
/// 多个苍/赫 不用互相仲裁：这里把它们的方向按距离和强度合成一个合力，越近的越强。
/// </summary>
public partial class JujutsuMotion : BallComponent
{
    public override int Id => 1002;

    public override string Type => "movement.jujutsu_motion";

    public override string DisplayName => "苍/赫牵引";

    public override string Description => "场上有苍/赫时接管移动：被苍吸、被赫推；没有就放行";

    /// <summary>被牵引时的速率（像素/秒）。离得越近越接近这个速度，远了按距离衰减。</summary>
    public float PullSpeed = 600f;

    /// <summary>移动链上的优先级。比普通移动大，才能排在它前面处理。</summary>
    public int Priority = MovePriority.Jujutsu;

    /// <summary>合力小于这个数就当"没有影响"、放行——不然在范围边缘上会被拖成龟速。</summary>
    public float MinPower = 0.05f;

    /// <summary>
    /// 吸住半径：球离**最近的**那颗苍/赫 近到这个距离以内就开始刹车（速度按 距离÷半径 缩），
    /// 贴到中心就是 0 —— 于是它停在苍/赫 身上，而不是冲过去再被拉回来地来回抖。
    /// 0 = 不刹车（那就还是会来回穿）。
    /// </summary>
    public float HoldRadius = 40f;

    private Ball _ball;

    /// <summary>这会儿是不是正被苍/赫 拽着（用来记、还"被抓走之前的速度"）。</summary>
    private bool _driving;

    /// <summary>接管之前那颗球自己的速度大小，放手时还回去。</summary>
    private float _baseSpeed;

    /// <summary>被抓住之前那颗球的速度（连方向一起记着，贴身停住之后松开也还能还原）。</summary>
    private Vector2 _baseVelocity;

    /// <summary>上一次牵引的方向：球正好压在苍/赫 中心、方向算不出来时接着用它。</summary>
    private Vector2 _lastPull;

    /// <summary>`PickOrb` 挑中的那颗苍/赫 有多远（没有就是 -1）。贴身刹车要用它。</summary>
    private float _targetDistance = -1f;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        PullSpeed = JsonTool.GetValue(parameters, "pull_speed", PullSpeed);
        Priority = JsonTool.GetValue(parameters, "priority", Priority);
        MinPower = JsonTool.GetValue(parameters, "min_power", MinPower);
        HoldRadius = JsonTool.GetValue(parameters, "hold_radius", HoldRadius);
    }

    /// <summary>装配时接线：把自己挂到移动链上，排在普通移动前面。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，牵引接不上。");
            return;
        }

        ball.Events.Register(EventName.move_step, new EventResponseFunction
        {
            priority = Priority,
            action = OnMoveStep,
        });
    }

    /// <summary>移动链轮到我了：场上有苍/赫 就自己驱动并阻塞普通移动；没有就返回 true 放行。</summary>
    private bool OnMoveStep(object arg)
    {
        if (_ball == null || arg is not float delta)
        {
            return true;
        }

        if (!PickOrb(out var direction, out float power) || power < MinPower)
        {
            EndDrive(); // 场上没有苍/赫（或者弱到没影响）：把方向盘还回去
            return true;
        }

        BeginDrive();

        // 越近越快：权重就是"功率"，上限 1（= PullSpeed）；
        // 但贴身之后要刹车，不然球会穿过苍球再被拉回来（见 HoldRadius）
        float speed = PullSpeed * Mathf.Min(1f, power) * HoldFactor();

        _lastPull = direction;
        _ball.Velocity = _lastPull * speed;

        // 推进：**撞到东西（墙）就贴着墙滑，不弹开**。
        // 这里不用 `BallMovement.Drive`（撞了就反射）——反射出来的速度下一帧又会被牵引硬设回去，
        // 于是球贴着墙"位置不动、速度满格"地抖。
        //
        // 判断"有没有被挡住"也不看 `MoveAndCollide` 的返回值：贴着墙的时候它可能返回 null
        // （位移被物理引擎的保险处理吞掉了），那样球就会一直顶着墙。直接比"想走多少 / 实际走了多少"最稳。
        var wanted = _ball.Velocity * (float)delta;
        var before = _ball.Position;
        _ball.MoveAndCollide(wanted);
        var moved = _ball.Position - before;

        if (moved.LengthSquared() < wanted.LengthSquared() * 0.9f)
        {
            // 被挡住了：能滑就沿"实际挪出去的那点"滑，一点都滑不动（正对着墙）就停在墙前
            _lastPull = moved.LengthSquared() > 0.0001f ? moved.Normalized() : Vector2.Zero;
            _ball.Velocity = _lastPull * speed;
        }

        return false; // 阻塞：这一帧的移动由苍/赫 说了算
    }

    /// <summary>
    /// 挑一颗**现在最管用的**苍/赫：按"强度 × 距离衰减"取最大的那一颗，
    /// 顺便给出这一帧该往哪边、多用力。只算"不是自己阵营"的、已经到位的、在范围内的。
    ///
    /// 为什么是"挑一颗"而不是"把几颗的力加起来"：加起来的话，两颗方向相反的苍/赫 会互相抵消，
    /// 球停在中间还会**逐帧翻转方向**——位置不动、速度满格地抖（两个五条互打时苍赫加倍，特别明显）。
    /// 挑一颗之后方向是稳的：球朝它走，它就变得更近、权重更大，赢家不会来回换。
    /// </summary>
    private bool PickOrb(out Vector2 direction, out float power)
    {
        direction = Vector2.Zero;
        power = 0f;
        _targetDistance = -1f;

        foreach (var node in GetTree().GetNodesInGroup(JujutsuOrb.OrbsGroup))
        {
            if (node is not JujutsuOrb orb || !orb.Active || orb.OwnerGroup == _ball.Group)
            {
                continue; // 不是自己该理的苍/赫、或者还在飞的：都不算
            }

            var toOrb = orb.GlobalPosition - _ball.GlobalPosition;
            float distance = toOrb.Length();

            if (orb.Range > 0f && distance > orb.Range)
            {
                continue; // 范围外
            }

            // 越近权重越大；Range = 0（全场）就是恒定强度
            float weight = orb.Strength * (orb.Range > 0f ? 1f - distance / orb.Range : 1f);
            if (weight <= power)
            {
                continue; // 没有现在这颗管用
            }

            // 方向：从球指向苍球。正好压在中心上时方向算不出来，就沿用上一次的方向，
            // 免得"贴到中心那一帧突然没方向 → 放行 → 球飞走 → 又被拉回来"地抖。
            var toOrbDirection = distance > 0.001f ? toOrb / distance : _lastPull;
            if (toOrbDirection == Vector2.Zero)
            {
                continue;
            }

            direction = orb.Kind == OrbKind.Attract ? toOrbDirection : -toOrbDirection;
            power = weight;
            _targetDistance = distance;
        }

        return power > 0f;
    }

    /// <summary>
    /// 贴身刹车系数：离最近的苍/赫 在吸住半径以内时返回 距离÷半径（贴到中心就是 0），
    /// 否则返回 1（正常牵引）。所以球是"越接近越慢，最后停在苍/赫 身上"。
    /// </summary>
    private float HoldFactor()
    {
        if (HoldRadius <= 0f || _targetDistance < 0f || _targetDistance >= HoldRadius)
        {
            return 1f;
        }

        return _targetDistance / HoldRadius;
    }

    /// <summary>开始被牵引：记下"被抓走之前的速度"，放手时好还回去。</summary>
    private void BeginDrive()
    {
        if (_driving)
        {
            return;
        }

        _driving = true;
        _baseVelocity = _ball.Velocity;
        _baseSpeed = _baseVelocity.Length();
        GD.Print($"[{Type}] {_ball.Name} 被苍/赫 抓住（记下原本的速度 {_baseSpeed:0.#}）");
    }

    /// <summary>
    /// 松手：方向按当时的算，**大小还成接管之前的**。
    /// （`1001` 的初速度只在第一帧写一次、之后不再归一化，不还回去的话，
    /// 那颗球会带着"被苍拉出来的速度"一直跑。）
    ///
    /// 球要是正被吸在苍/赫 身上（速度已经是 0），方向就从"被抓住之前的速度"里取——
    /// 不然它会被永远留在 0 速度上，再也不会动。
    /// </summary>
    private void EndDrive()
    {
        if (!_driving)
        {
            return;
        }

        _driving = false;

        if (_baseSpeed <= 0f)
        {
            return; // 被抓之前它本来就没在动（比如刚出场），交给普通移动自己去起速度
        }

        var direction = _ball.Velocity.LengthSquared() > 0.001f
            ? _ball.Velocity.Normalized()
            : _baseVelocity.Normalized();

        if (direction == Vector2.Zero)
        {
            return;
        }

        _ball.Velocity = direction * _baseSpeed;
        GD.Print($"[{Type}] {_ball.Name} 脱离苍/赫（速度还原成 {_baseSpeed:0.#}）");
    }
}
