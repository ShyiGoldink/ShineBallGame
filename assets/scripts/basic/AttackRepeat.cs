/// <summary>
/// 已经在攻击状态里、又出一招时，这一招跟当前那一招怎么处。
/// 和控制状态的 `ControlRepeat` 是一个思路（那边是 重置 / 叠加 / 不理会）。
/// </summary>
public enum AttackRepeat
{
    /// <summary>覆盖：新招接管——招式名、时长、动画都按新的来，旧招就这么结束。</summary>
    Replace,

    /// <summary>延长：**不换招**，把新招的时长加到剩余时间上（动画继续播，不重播）。</summary>
    Extend,

    /// <summary>
    /// 打断：先把当前这一招**打断**（发一条 `attack_interrupted` 通知，让那一招有机会收尾，
    /// 比如取消还没生效的前摇），然后从头开始演新招。
    /// </summary>
    Interrupt,
}
