using Godot;

/// <summary>
/// 减速：挂在**被打的那颗球**身上（攻击方开局用自己的 enemycomponents 挂过来）。
///
/// 它跟移动类组件是同一套路子，只是各管一个状态：
///   状态变成「移动」→ `1001` 驱动球；状态变成「受控」→ 这个组件接手驱动球，只是慢一些。
/// 所以减速**不是靠挨打触发的**：蛋碰到球的时候把球切成受控状态（`Ball.BeControlled`），
/// 这里听见状态变化就接管这一段路的移动——方向不变，速度压到原先的 30%。
///
/// 球身上没挂这个组件时，受控就是"定身"（没人驱动它）；挂上了就是"减速"。
/// 换句话说：受控状态只说"谁被接管了"，**接管之后怎么动，由控制类组件决定**。
/// </summary>
public partial class Slow : BallComponent
{
    public override int Id => 6001;

    public override string Type => "control.slow";

    public override string Description => "受控期间接手驱动球：方向不变，速度压到原先的 30%";

    /// <summary>注册到状态变化事件时的优先级（别的状态监听都是 0，一般不用改）。</summary>
    public int Priority;

    /// <summary>速度压到原先的多少。0.3 = 慢到三成。</summary>
    public float SpeedScale = 0.3f;

    /// <summary>
    /// 控制类别。将来一颗球上可能挂多个控制类组件（减速、眩晕……），
    /// **同类别之间才谈得上"谁更强"**，不同类别应该各管各的。
    /// 仲裁规则见 `BallControl.PickDriver`。
    /// </summary>
    public string Category = "slow";

    /// <summary>类别的优先级：不同类别之间谁说了算（大的赢）。眩晕那类会配得比减速高。</summary>
    public int CategoryPriority = 100;

    /// <summary>同类别的强度（大的赢）。减速这里默认按"减得多狠"算：0.3 倍 → 强度 70。</summary>
    public int Strength = -1;

    private Ball _ball;
    private bool _controlling;

    /// <summary>接管那一刻的速度大小，用来还原。0 表示没在接管。</summary>
    private float _baseSpeed;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Priority = JsonTool.GetValue(parameters, "priority", Priority);
        SpeedScale = JsonTool.GetValue(parameters, "speed_scale", SpeedScale);
        Category = JsonTool.GetValue(parameters, "category", Category);
        CategoryPriority = JsonTool.GetValue(parameters, "category_priority", CategoryPriority);
        Strength = JsonTool.GetValue(parameters, "strength", Strength);
    }

    public override string ControlCategory => Category;

    public override int ControlCategoryPriority => CategoryPriority;

    /// <summary>没显式配 strength 就按减速比例折算：减得越狠越强（0.3 倍 → 70）。</summary>
    public override int ControlStrength =>
        Strength >= 0 ? Strength : Mathf.RoundToInt((1f - SpeedScale) * 100f);

    /// <summary>
    /// 装配时接线：订"状态变化"（受控一到就接手方向盘）。
    /// 如果挂上来的时候球已经在受控里（少见），也照样按减速处理。
    /// </summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，接不了方向盘。");
            return;
        }

        _ball.Events.Register(EventName.state_changed, new EventResponseFunction { priority = Priority, action = OnStateChanged });

        if (_ball.State == BallState.Controlled)
        {
            BeginControl();
        }
    }

    private bool OnStateChanged(object arg)
    {
        bool controlled = arg is BallState state && state == BallState.Controlled;

        if (controlled)
        {
            BeginControl();
        }
        else if (_controlling)
        {
            EndControl();
        }

        return true;
    }

    /// <summary>接手：记下"原先的速度"，把速度压到 30%。</summary>
    private void BeginControl()
    {
        _controlling = true;
        _baseSpeed = _ball.Velocity.Length();

        if (_baseSpeed <= 0f)
        {
            return; // 还没动起来的球没什么可减的
        }

        _ball.Velocity = _ball.Velocity.Normalized() * (_baseSpeed * SpeedScale);
        GD.Print($"[{Type}] {_ball.Name} 减速：{_baseSpeed:0.#} → {_baseSpeed * SpeedScale:0.#}");
    }

    /// <summary>撒手：速度还原（方向按现在的算，中途撞过墙也一样）。</summary>
    private void EndControl()
    {
        _controlling = false;

        if (_baseSpeed > 0f && _ball.Velocity.LengthSquared() > 0f)
        {
            _ball.Velocity = _ball.Velocity.Normalized() * _baseSpeed;
            GD.Print($"[{Type}] {_ball.Name} 减速结束");
        }

        _baseSpeed = 0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        // 受控期间由我驱动球（移动类组件在受控状态里是不动的）。
        // 但方向盘只有一根：如果这会儿别人（比如眩晕）更强，就别抢——这就是"阻塞"。
        if (_controlling && BallControl.PickDriver(_ball) == this)
        {
            BallMovement.Drive(_ball, (float)delta);
        }
    }
}
