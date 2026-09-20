public static class EventName
{
    public const string take_damage = "TAKEDAMAGE";

    /// <summary>状态变化。载荷：BallState（新的状态）。</summary>
    public const string state_changed = "STATECHANGED";

    /// <summary>
    /// 血量变了。载荷：float（**这次变了多少**：正数回血、负数掉血；当前血量读 `Ball.Hp`）。
    ///
    /// 想"只在受伤 / 回血的时候做点什么"就订这个，别每帧去比血量——
    /// 反转术式（`4005`）就是靠它被叫醒的：平时它连 `_PhysicsProcess` 都关着。
    /// 通知类事件，处理函数一律返回 true（返回 false 会把后面组件的收听堵掉）。
    /// </summary>
    public const string hp_changed = "HPCHANGED";

    /// <summary>
    /// 移动链：**球每帧在移动状态下发的"这一帧谁来驱动我"**。载荷：float（这一帧的秒数）。
    ///
    /// 按优先级从大到小跑，**谁先处理谁就有权把这一帧的移动接手**：处理完返回 false
    /// 就把后面的挡住（苍/赫 在场时就是这么把普通移动阻塞掉的），返回 true 就是放行。
    /// 只有移动状态会发这个事件，所以"登场/受控/攻击/死亡时谁来驱动"这套语义天然不冲突：
    /// 受控状态下的驱动权还是 `BallControl` 那套仲裁说了算。
    /// </summary>
    public const string move_step = "MOVESTEP";

    /// <summary>
    /// 出招：**球开始演一招时发**（含换招）。载荷：string（招式名，可能是空的）。
    ///
    /// 外观层（`SpineLook`）收到它就去招式表里找这一段动画叫什么，找不到再退回按状态找。
    /// 状态没变（本来就是攻击状态，只是换了一招）的时候，只有这个事件会响——
    /// 所以"哪一招"这件事不能只靠 `state_changed` 知道。
    /// </summary>
    public const string attack_started = "ATTACKSTARTED";

    /// <summary>
    /// 攻击被打断：**当前这一招被新的招打断时发**（`AttackRepeat.Interrupt`）。载荷：string（被打算的那一招）。
    /// 给"还没生效的效果"一个收尾的机会（比如取消前摇、关掉判定）。
    /// 注意：正常演完、或者被覆盖（`Replace`）都不会发这个。
    /// </summary>
    public const string attack_interrupted = "ATTACKINTERRUPTED";

}
