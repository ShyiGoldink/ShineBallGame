using Godot;
using System;

/// <summary>
/// 所有行为组件的基类。它就是个 Node，平时挂在小球下面，用的就是 Godot 自带的组件能力。
///
/// 目前只保留自描述这一部分：组件用 Id / Type 说明自己是谁，用 Requirements 声明前置组件。
/// 装配（挂载、绑定、注册事件）交给之后的装配工厂处理，
/// 所以基类里没有 Bind，也没有生命周期方法。
/// </summary>
public abstract partial class BallComponent : Node
{
    /// <summary>唯一编号。约定：1xxx 移动，2xxx 攻击，3xxx 防御，4xxx 行为。</summary>
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
}
