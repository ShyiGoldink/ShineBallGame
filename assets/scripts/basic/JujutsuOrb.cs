using Godot;

/// <summary>
/// 苍球 / 赫球 的共同底子——场上的"术式球"。
///
/// **它不是球**：不进 `balls` 组、不参与胜负判定、没有血量（跟蛋一个待遇）。
/// 它做的事就一件：待在场上，让"苍/赫牵引"组件（`1002 movement.jujutsu_motion`）
/// 能读到"我在哪、我是谁放的、我是苍还是赫"。
///
/// 进场景树之前调 `SetOwner(阵营)` 把自己登记进 `orbs` 组，之后牵引组件每帧去这个组里找它。
/// 视觉（图、缩放、动画）、存活时间、能不能被打散，都是将来那个预制体自己的事——
/// 要感知碰撞就自己挂个 `Area2D` 子节点，这个底类不预设。
/// </summary>
public abstract partial class JujutsuOrb : Node2D
{
    /// <summary>苍球 / 赫球 都挂在这个 node group 里（和蛋的 `eggs` 一个套路，找法统一）。</summary>
    public const string OrbsGroup = "orbs";

    /// <summary>谁放的（阵营）。牵引只会影响**别的阵营**的球，不拽自己人。</summary>
    public int OwnerGroup { get; private set; }

    /// <summary>苍（吸）还是赫（推）。</summary>
    public OrbKind Kind = OrbKind.Attract;

    /// <summary>强度：越大在合力里越"抓得住"（多个苍/赫 按它加权）。默认 1。</summary>
    public float Strength = 1f;

    /// <summary>影响半径。0 = 全场；写了大小的话，范围外的一律不受影响。</summary>
    public float Range;

    /// <summary>
    /// 这会儿算不算"正在影响场上的球"。
    /// 还在飞的苍/赫 应该先关着（`false`），飞到位、停下来那一刻再打开——
    /// 牵引组件（`1002`）只理 `Active` 的，所以"飞过去"这一段不会拽人。
    /// </summary>
    public bool Active = true;

    /// <summary>
    /// 装配时调（**进场景树之前**）：记下阵营，并把自己挂进 `orbs` 组。
    /// 和蛋的 `Egg.SetOwner` 是同一个位置、同一个道理。
    /// </summary>
    public void SetOwner(int group)
    {
        OwnerGroup = group;
        AddToGroup(OrbsGroup);
    }
}
