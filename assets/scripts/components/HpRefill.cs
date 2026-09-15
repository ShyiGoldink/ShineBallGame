using Godot;

/// <summary>
/// 打空就回满（`4007`）：血量掉到 `trigger_hp` 及以下时，等 `delay` 秒把人**回满**。
/// 沙包球拿它当"打不烂的靶子"：一巴掌拍到底 → 停一下（让人看得见到底了）→ 满血重来。
///
/// 和反转术式一样，它是**被血量变化叫醒的**，不是每帧盯着血看：平时 `_PhysicsProcess`
/// 关着（见 `_Ready`），`hp_changed` 响了才看一眼；只有进了"等那 `delay` 秒"的阶段才打开它。
/// `delay = 0` 就是打到底的同一瞬间回满（那就看不到 1 血那一帧了）。
///
/// 人不在了（已经死亡）就不回满——死亡是终点，回满血也不该把人从死亡状态里拉回来。
/// </summary>
public partial class HpRefill : BallComponent
{
    public override int Id => 4007;

    public override string Type => "behavior.hp_refill";

    public override string DisplayName => "回满血";

    public override string Description => "血量掉到 trigger_hp 及以下时，等 delay 秒回满（沙包球用它当不烂的靶子）";

    /// <summary>掉到这个血量及以下就触发。默认 1（和沙包受伤的保底对齐）。</summary>
    public float TriggerHp = 1f;

    /// <summary>触发之后等几秒再回满，0 = 立刻回满。默认 1 秒，让人看得见"被打到底了"。</summary>
    public float Delay = 1f;

    private Ball _ball;

    /// <summary>这会儿正在等那几秒。没在等的时候 `_PhysicsProcess` 是关着的。</summary>
    private bool _waiting;

    /// <summary>还要等几秒。</summary>
    private float _left;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        TriggerHp = JsonTool.GetValue(parameters, "trigger_hp", TriggerHp);
        Delay = JsonTool.GetValue(parameters, "delay", Delay);
    }

    /// <summary>装配时接线：只留一根"血量变了叫我"的线。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，回不了血。");
            return;
        }

        ball.Events.Register(EventName.hp_changed, new EventResponseFunction { priority = 0, action = OnHpChanged });
    }

    /// <summary>
    /// 进场景树之后把"平时不跑"落实。**必须在这里关**：脚本里有 `_PhysicsProcess` 时，
    /// 引擎会在节点进树时自动打开它，装配期（`Bind`）关的那次会被盖掉。
    /// </summary>
    public override void _Ready()
    {
        if (!_waiting)
        {
            SetPhysicsProcess(false);
        }
    }

    private bool OnHpChanged(object arg)
    {
        // 通知类事件：不管这次干不干活都要放行
        if (_waiting || _ball == null)
        {
            return true;
        }

        if (_ball.State == BallState.Dead)
        {
            return true; // 人已经没了：回满也不该把人救回来
        }

        if (_ball.Hp > TriggerHp)
        {
            return true; // 还没打到底
        }

        if (Delay <= 0f)
        {
            Refill(); // 立刻回满
            return true;
        }

        _waiting = true;
        _left = Delay;
        SetPhysicsProcess(true); // 只有等的这几秒才需要每帧
        GD.Print($"[{Type}] {_ball.Name} 被打到底了（剩 {_ball.Hp:0.#} 血），{Delay:0.##} 秒后回满");
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_waiting || _ball == null || delta <= 0.0)
        {
            return;
        }

        if (_ball.State == BallState.Dead)
        {
            Abandon("人已经没了");
            return;
        }

        _left -= (float)delta;
        if (_left > 0f)
        {
            return;
        }

        Refill();
    }

    /// <summary>回满，然后回到"一次都不跑"的状态。</summary>
    private void Refill()
    {
        _waiting = false;
        SetPhysicsProcess(false);

        float before = _ball.Hp;
        _ball.Hp = _ball.MaxHp; // 这一步又会喊一次 hp_changed，但那时血已经满了，不会再触发
        GD.Print($"[{Type}] {_ball.Name} 回满血：{before:0.#} → {_ball.Hp:0.#}");
    }

    /// <summary>等不下去了（人没了），收摊。</summary>
    private void Abandon(string why)
    {
        _waiting = false;
        SetPhysicsProcess(false);
        GD.Print($"[{Type}] {_ball.Name} 不回满了：{why}");
    }
}
