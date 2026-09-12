/// <summary>
/// 小球的状态。定死：种类和数量不开放给用户自定义，
/// 因为所有组件都靠"现在是什么状态"来决定自己该不该干活。
/// </summary>
public enum BallState
{
    /// <summary>登场：刚上场，还没开始行动。</summary>
    Spawn,

    /// <summary>移动：自己动的球。移动类组件只在这个状态里驱动小球。</summary>
    Move,

    /// <summary>受控：控制类组件只在这个状态里响应输入。</summary>
    Controlled,

    /// <summary>攻击：可能会移动，也可能不动。</summary>
    Attack,

    /// <summary>死亡：终点，不再接受任何状态切换。</summary>
    Dead,
}
