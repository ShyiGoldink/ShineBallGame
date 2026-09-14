using Godot;
using System;

/// <summary>
/// 所有行为组件的基类。它就是个 Node，挂在球下面（蛋那种普通节点也复用同一套写法）。
///
/// 组件只做两件事：
/// 1. **自描述**：Id / Type / Description / Requirements / ApplyParams；
/// 2. **在 `Bind` 里接线**：注册事件、连信号、改自己的形状。
///
/// 装配器只管"按编号建出来 → 填参数 → 挂上去 → 调 Bind"，它**不认识任何具体组件**
/// ——这就是"加组件不用改装配器"的原因。
///
/// 两个入口的分工：
/// * `Bind(ball, configId)`：**装配时**调，那时球还没进场景树。注册事件、连信号、改形状都放这里。
///   它在"所有组件都挂完"之后、摆血条之前跑，所以 `5001` 改过的碰撞圈后面能读到。
/// * `_Ready()`：球**进场景树之后**引擎自己调。必须"在树里"才能做的事（查兄弟节点之类）放这里。
///
/// `configId` 是"哪颗球的 Json 配出了我"：自己身上的组件就是这颗球，
/// 从 `enemycomponents` 挂过来的则是**配它的那颗球**。找音效、找素材用它，别用 `GetParent()` 那颗。
/// </summary>
public abstract partial class BallComponent : Node
{
    /// <summary>唯一编号。约定：1xxx 移动，2xxx 攻击，3xxx 防御，4xxx 行为，5xxx 形态，6xxx 控制。</summary>
    public abstract int Id { get; }

    /// <summary>类型名，形如 "attack.contact"。Json 和文档里用它指代这个组件。</summary>
    public abstract string Type { get; }

    /// <summary>显示名，用于文档和调试输出。</summary>
    public virtual string DisplayName => Type;

    /// <summary>这个组件是做什么的，一句话说清。</summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// 前置组件：没装配它，这个组件就跑不起来。写的是依赖，不是处理顺序。
    /// 受伤和无敌现在都是独立的组件，彼此不互相依赖，
    /// 它们只是在小球的事件链上处在不同的优先级。
    /// </summary>
    public virtual string[] Requirements => Array.Empty<string>();

    /// <summary>
    /// 装配时把 Json 里这个组件的参数读进自己的字段。
    /// 没有参数（比如普通受伤）就不用管它。缺的参数一律用组件里的默认值。
    /// </summary>
    public virtual void ApplyParams(Godot.Collections.Dictionary parameters)
    {
    }

    /// <summary>
    /// 装配时接线：注册事件、连信号、填自己的字段。
    /// 默认什么都不做——不需要接线的组件（纯参数型）不用管它。
    /// </summary>
    public virtual void Bind(Ball ball, string configId)
    {
    }

    // ---------- 控制类组件用的一小组属性（别的组件不用管）----------
    //
    // 一颗球上可能同时挂着好几个"想要方向盘"的组件（减速、眩晕……），但球只有一个移动，
    // 所以必须有确定的规则选出一个：见 `BallControl.PickDriver`。
    // 不是控制类的组件保持默认值（ControlCategory 为 null）就会被忽略。

    /// <summary>控制类别，比如 "slow" / "stun"。**只有同类别之间才比强度**。</summary>
    public virtual string ControlCategory => null;

    /// <summary>类别的优先级：不同类别之间谁说了算（大的赢）。</summary>
    public virtual int ControlCategoryPriority => 0;

    /// <summary>同类别的强度（大的赢）：减速就是"减得更狠"、眩晕就是"晕得更久"。</summary>
    public virtual int ControlStrength => 0;
}
