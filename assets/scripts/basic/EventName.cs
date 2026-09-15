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

}
