/// <summary>
/// 受控期间又被要求受控时怎么处理。
/// </summary>
public enum ControlRepeat
{
    /// <summary>重置：剩余时间直接换成这次传进来的时间。</summary>
    Reset,

    /// <summary>增加：在剩余时间上叠加这次传进来的时间。</summary>
    Add,

    /// <summary>不理会：维持原来的剩余时间。</summary>
    Ignore,
}
