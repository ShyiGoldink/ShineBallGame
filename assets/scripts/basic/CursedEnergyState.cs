/// <summary>
/// 咒力池的状态。**只存在咒力基础组件（`7001`）里**——别的组件要用就来问它，
/// 不自己存一份（存两份就一定会出现"这个说熔断了、那个说没有"）。
///
/// 它只记"**现在处于哪种模式**"（互斥：熔断期间就不是领域），具体加多少由各个组件自己决定。
/// **黑闪后不是一种模式，是一层一层叠起来的加成**，所以它不走这个枚举，
/// 走咒力池的层数与加成表（`CursedEnergy.BlackFlashStacks` / `EnterBlackFlash`）——
/// 否则熔断期间打出黑闪会把熔断顶掉。
///
/// Json 里写状态名（不区分大小写，下划线可有可无）：
/// `"normal"` / `"domain_boost"` / `"burnout"` / `"black_flash"`。
/// </summary>
public enum CursedEnergyState
{
    /// <summary>正常。</summary>
    Normal,

    /// <summary>领域中增强。</summary>
    DomainBoost,

    /// <summary>术式熔断。</summary>
    Burnout,

    /// <summary>
    /// 黑闪后状态提升。**黑闪现在不写这个值了**（黑闪走层数，见类说明），
    /// 留着是给"刚打出黑闪"的表现用（动画、飘字之类）——真要写它，记得它和熔断互斥。
    /// </summary>
    BlackFlash,
}
