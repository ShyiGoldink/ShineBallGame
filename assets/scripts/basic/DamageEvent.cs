using Godot;

/// <summary>
/// 一次伤害事件：谁打的、被谁打、本来多少、还剩多少。
///
/// 受伤链上的处理函数（按 `DamagePriority` 从大到小跑）可以：
///
/// * 读 `Original` —— "本来该打多少"（护盾按原始值吃伤害就靠它）；
/// * 改 `Amount` —— 把伤害改小或改大，**后面的处理函数看到的就是改过的值**
///   （减伤、护盾扣掉一部分再放行）；`4001` 最后按 `Amount` 扣血；
/// * 看 `Source` / `SourceId` / `SourceType` —— 区分"这是什么打来的"
///   （比如只有某个来源的伤害才上中毒）；
/// * 返回 false —— 直接堵死整条链（无敌就是这么做的）。
/// </summary>
public sealed class DamageEvent
{
    /// <summary>挨打的球。</summary>
    public readonly Ball Target;

    /// <summary>还剩多少伤害。处理函数可以改它，改完往后传。</summary>
    public float Amount;

    /// <summary>原始伤害，别改；想知道"本来多少"时读它。</summary>
    public readonly float Original;

    /// <summary>打人的那颗球。蛋这种"埋伏"没有球当来源时是 null。</summary>
    public readonly Ball Source;

    /// <summary>来源的球 id（文件夹名）。蛋埋伏时填的是下蛋那颗球的 id。</summary>
    public readonly string SourceId;

    /// <summary>打出这一下的是哪种组件，比如 "attack.collision" / "attack.shift"。环境伤害可以是 null。</summary>
    public readonly string SourceType;

    public DamageEvent(Ball target, float amount, string sourceType = null, Ball source = null, string sourceId = null)
    {
        Target = target;
        Amount = amount;
        Original = amount;
        SourceType = sourceType;
        Source = source;
        SourceId = sourceId ?? source?.Id;
    }
}
