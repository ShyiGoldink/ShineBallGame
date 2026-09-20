using Godot;

/// <summary>
/// 反转术式（`4005`）：**血不满就自己回血**，回多少血就烧多少咒力。
///
/// 效率写在咒力池里（`jujutsu.cursed_energy` 的 `reverse_rate`，单位是"每秒回多少血"）：
/// * **效率是 0 就等于不会反转术式**——组件挂着也不干活，这就是"会不会"的开关；
/// * 效率不为 0 才算能用，然后按这个速率每秒回 n 点血。
///
/// **它是被血量变化叫醒的，不是每帧盯着血量看**：
/// 平时它把自己的 `_PhysicsProcess` 关掉（`SetPhysicsProcess(false)`，见 `_Ready`），
/// 引擎根本不会来调它；
/// 谁的血量一变（球身上的 `hp_changed` 事件），它才看一眼——
/// 血不满、这会儿没在回血、人还没死，就地开回血，并把 `_PhysicsProcess` 打开。
/// 血回满了、或者咒力不够了，就再把自己关掉，回到"一次都不跑"的状态。
/// 所以空闲时的开销是**零次调用**，不是"每帧做一次判断"。
///
/// 烧咒力：**回多少血扣多少血 × 咒力消耗倍率**（倍率就是咒力池的 `cost_rate`，
/// 六眼那种 0.01 就是只花百分之一）。这里只报"我这次回了多少血"，乘倍率是池子里的事。
/// **先付账后回血**：付不起这一帧就不回、并且把术式停掉
/// （不是"挂着等咒力回来"——那又变成每帧盯账了；重新回血的时机是下一次血量变化）。
/// </summary>
public partial class ReverseTechnique : BallComponent
{
    public override int Id => 4005;

    public override string Type => "behavior.reverse_technique";

    public override string DisplayName => "反转术式";

    public override string Description => "血不满就按反转术式效率回血，回多少血烧多少咒力（效率 0 = 不会用）";

    /// <summary>前置组件：效率写在咒力池里，烧的也是它的咒力。</summary>
    public override string[] Requirements => new[] { CursedEnergy.TypeName };

    private Ball _ball;
    private CursedEnergy _pool;

    /// <summary>这会儿正在回血没有。没在回血时 `_PhysicsProcess` 是关着的。</summary>
    private bool _healing;

    /// <summary>装配时接线：只留一根"血量变了叫我"的线。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;

        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，反转术式不生效。");
            return;
        }

        _pool = FindPool(ball);
        if (_pool == null)
        {
            // 正常走不到这里：装配器按 Requirements 拦过了。
            GD.PushError($"[{Type}] 球上没有 {CursedEnergy.TypeName}，反转术式不生效。");
            return;
        }

        ball.Events.Register(EventName.hp_changed, new EventResponseFunction { priority = 0, action = OnHpChanged });

        GD.Print(_pool.ReverseRate > 0f
            ? $"[{Type}] {ball.Name} 的反转术式就绪：血一不满就每秒回 {_pool.ReverseRate:0.#} 点（每点血花 {_pool.CostRate:0.###} 咒力）"
            : $"[{Type}] {ball.Name} 的反转术式效率是 0（咒力池的 reverse_rate），等于不会用。");
    }

    /// <summary>
    /// 进场景树之后：把"平时不跑"落实。
    ///
    /// **必须在这里关，不能只在装配期关**：脚本里写了 `_PhysicsProcess` 的时候，
    /// 引擎会在节点进树时把它自动打开，装配期（`Bind`，那会儿球还没进树）关的那次会被盖掉。
    /// </summary>
    public override void _Ready()
    {
        if (!_healing)
        {
            SetPhysicsProcess(false);
        }
    }

    /// <summary>
    /// 血量一变就被叫醒。**这里是唯一判断"该不该开始回血"的地方**，
    /// 回血过程中血量每帧都在变，但 `_healing` 已经是 true，所以不会重复开。
    /// </summary>
    private bool OnHpChanged(object arg)
    {
        // 通知类事件：不管这次干不干活都要放行，别把后面组件的收听堵掉
        if (_healing || _ball == null || _pool == null)
        {
            return true;
        }

        if (_pool.ReverseRate <= 0f)
        {
            return true; // 效率 0：不会反转术式
        }

        if (_ball.State == BallState.Dead)
        {
            return true; // 人都没了，回血没意义（也别把死人从 0 血拉起来）
        }

        if (_ball.Hp >= _ball.MaxHp)
        {
            return true; // 血是满的，不用回
        }

        _healing = true;
        SetPhysicsProcess(true); // 从这一刻起才开始每帧工作
        GD.Print($"[{Type}] {_ball.Name} 开始回血（每秒 {_pool.ReverseRate:0.#} 点）");
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_healing || _ball == null || _pool == null || delta <= 0.0)
        {
            return;
        }

        float missing = _ball.MaxHp - _ball.Hp;
        if (missing <= 0f || _ball.State == BallState.Dead)
        {
            Stop("血回满了");
            return;
        }

        // 这一帧回多少：按每秒量算，但别回过头
        float heal = Mathf.Min(_pool.ReverseRate * (float)delta, missing);
        if (heal <= 0f)
        {
            return;
        }

        // 先付账：回多少血就花多少血 × 咒力消耗倍率（倍率由池子乘，这里报回血的量）
        if (!_pool.TrySpend(heal))
        {
            Stop("咒力不够");
            return;
        }

        _ball.Hp += heal; // 这一步又会喊一次 hp_changed，但那时 _healing 已经是 true，不会被当成"重新开始"
    }

    /// <summary>停下回血，把自己关回"一次都不跑"的状态，等下一次血量变化再叫醒。</summary>
    private void Stop(string why)
    {
        _healing = false;
        SetPhysicsProcess(false);
        GD.Print($"[{Type}] {_ball?.Name} 停止回血：{why}");
    }

    /// <summary>按 `Type` 名在球身上找咒力池——依赖声明的就是这个名字。</summary>
    private static CursedEnergy FindPool(Ball ball)
    {
        foreach (var child in ball.GetChildren())
        {
            if (child is CursedEnergy pool)
            {
                return pool;
            }
        }

        return null;
    }
}
