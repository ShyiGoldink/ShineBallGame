using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 咒力基础组件（`7001`）：一个咒术师的**咒力池**，也是咒力那一套组件的公共底子。
///
/// 咒术组件（无下限、反转术式……）都以它为前提：自己的 `Requirements` 里写上
/// `"jujutsu.cursed_energy"`，装配器就会检查"球上有没有这个池子"，
/// 然后在自己的 `Bind` 里按 `Type` 名把它找出来用。
///
/// 它只管池子本身，不含任何具体术式：
/// * **咒力量**（`Amount`，开局满值）和当前还剩多少（`Current`）；
/// * **咒力恢复速度**（`Regen`，每秒自动回，回到上限为止）；
/// * **状态**（`State`：正常 / 领域中增强 / 术式熔断 / 黑闪后状态提升）——
///   只存在这里，别的组件要用就来问，不自己存一份；
/// * **咒力消耗倍率**（`CostRate`，咒力操控程度）：实际消耗 = 名义消耗 × 这个值，
///   六眼那种可以配 0.01（只花百分之一）；
/// * **反转术式效率**（`ReverseRate`）：**每秒回多少血**。实际值 = 基础值 + 黑闪加成，
///   用不用由反转术式那个组件（`4005`）决定——**0 就是"不会反转术式"**。
///
/// **黑闪后的加成走"层数 + 每层加成表"，不走状态**（见 `EnterBlackFlash`）：
/// * `BlackFlashStacks`：本局打出的黑闪次数，累加、不清零。「黑闪后状态」就是它大于 0；
/// * Json 的 `black_flash` 表：**属性名 → 每层加多少**，要加新加成就多写一行；
/// * 池子自己管的属性（反转术式效率 `reverse_rate`、咒力恢复 `cursed_regen`、
///   消耗倍率 `cost_rate`）直接算进最终值，消费者一个字都不用改；
/// * 别人管的属性（伤害、护盾上限……）用 `Bonus(名)` / `Multiplier(名)` 来拿一格。
///
/// 为什么加成不塞进 `State`：**状态是互斥的，加成是叠加的**。黑闪后要"永久叠、持续生效"，
/// 而熔断/领域是"此时此刻是哪种模式"；塞进同一个枚举会互相顶掉——比如熔断期间打出黑闪，
/// 状态被改成"黑闪后"，无下限的熔断判断立刻失效、又开始回盾。
///
/// 给人用的两个口子：`TrySpend(名义消耗)` 花咒力（会乘倍率，不够就返回 false、不动值）、
/// `Restore(量)` 补咒力。**熔断不在这里拦人**：`TrySpend` 只看够不够，
/// 术式要不要在熔断期间停手由术式自己看 `BurnedOut` 决定——因为有的东西
/// （比如反转术式）熔断期间照样能用。
/// </summary>
public partial class CursedEnergy : BallComponent
{
    /// <summary>类型名。咒术组件报依赖、"按 Type 找兄弟"都用这个字符串，写成常量两边不会写歪。</summary>
    public const string TypeName = "jujutsu.cursed_energy";

    public override int Id => 7001;

    public override string Type => TypeName;

    public override string DisplayName => "咒力";

    public override string Description => "咒力池：咒力量 / 恢复速度 / 状态 / 消耗倍率 / 反转术式效率 / 黑闪层数与加成";

    /// <summary>加成表里这几个属性名。查询和配置都按它们来，写成常量免得两边写歪。</summary>
    public const string ReverseRateKey = "reverse_rate";   // 反转术式效率（每秒回多少血）
    public const string CursedRegenKey = "cursed_regen";   // 咒力恢复速度（每秒）
    public const string CostRateKey = "cost_rate";         // 咒力消耗倍率

    /// <summary>伤害。池子不管伤害，这个名字是给平A那类组件查加成用的。</summary>
    public const string DamageKey = "damage";

    /// <summary>咒力量：咒术师的咒力总值，也是开局时的量。</summary>
    public int Amount = 1000;

    /// <summary>咒力恢复速度：每秒自动回多少（回到上限为止）。0 = 不回。</summary>
    public int Regen;

    /// <summary>咒力消耗倍率（咒力操控程度）：实际消耗 = 名义消耗 × 这个值。默认 1（不省也不多花）。</summary>
    public float CostRate = 1f;

    /// <summary>
    /// 反转术式效率的**基础值**（Json 的 `reverse_rate`）：**每秒回多少血**。
    /// **0 = 不会反转术式**（挂了那个组件也不干活），所以默认就是 0——填了才"能用"。
    /// 实际用的是 `ReverseRate`（基础值 + 黑闪加成），消费者读那个就行。
    /// </summary>
    public float BaseReverseRate;

    /// <summary>每层黑闪给某个属性加多少（Json 的 `black_flash` 表：属性名 → 每层值）。</summary>
    private readonly Dictionary<string, float> _blackFlashPerStack = new();

    private Ball _ball;
    private CursedEnergyState _state = CursedEnergyState.Normal;

    /// <summary>这一轮干涸报过警了没有（见 `TrySpend` 里那句日志的说明）。</summary>
    private bool _warnedEmpty;

    /// <summary>当前还剩多少咒力。恢复是连续的，所以内部按浮点记，显示时取整。</summary>
    private float _current;

    /// <summary>现在还剩多少咒力。</summary>
    public float Current => _current;

    /// <summary>咒力上限（就是 `Amount`）。</summary>
    public float Max => Amount;

    /// <summary>是不是术式熔断中。术式组件要用就问这个，别自己存一份状态。</summary>
    public bool BurnedOut => _state == CursedEnergyState.Burnout;

    /// <summary>本局打出的黑闪次数。累加、不清零，「黑闪后状态」就是它大于 0。</summary>
    public int BlackFlashStacks { get; private set; }

    /// <summary>**现在实际**的反转术式效率：基础值 + 黑闪加成。反转术式组件读的就是它。</summary>
    public float ReverseRate => BaseReverseRate + Bonus(ReverseRateKey);

    /// <summary>**现在实际**的咒力恢复速度：基础值 + 黑闪加成。</summary>
    public float RegenNow => Regen + Bonus(CursedRegenKey);

    /// <summary>**现在实际**的咒力消耗倍率：基础值 + 黑闪加成（夹在 0 以上）。</summary>
    public float CostRateNow => Mathf.Max(0f, CostRate + Bonus(CostRateKey));

    /// <summary>黑闪给这个属性带来的**加成值**（层数 × 每层值）。表里没配这个属性就是 0。</summary>
    public float Bonus(string key) => BlackFlashStacks * PerStack(key);

    /// <summary>黑闪给这个属性带来的**倍率**（1 + 加成值）——"伤害 +10%/层"这种乘算用它。</summary>
    public float Multiplier(string key) => 1f + Bonus(key);

    private float PerStack(string key) =>
        _blackFlashPerStack.TryGetValue(key, out float value) ? value : 0f;

    /// <summary>
    /// 状态：正常 / 领域中增强 / 术式熔断 / 黑闪后状态提升。
    /// 换状态就换这里（全项目只有这一份），换的时候打一句日志方便对数。
    /// </summary>
    public CursedEnergyState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            GD.Print($"[{TypeName}] {Who} 的咒力状态改成「{StateText(_state)}」");
        }
    }

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Amount = JsonTool.GetValue(parameters, "amount", Amount);
        Regen = JsonTool.GetValue(parameters, "regen", Regen);
        CostRate = JsonTool.GetValue(parameters, "cost_rate", CostRate);
        BaseReverseRate = JsonTool.GetValue(parameters, "reverse_rate", BaseReverseRate);

        // 黑闪的加成表：属性名 → 每层加多少。名字是自由的（池子只认自己的那三个，
        // 别的名字留给消费方来问），所以这里不挑不拣，照单收下。
        _blackFlashPerStack.Clear();
        var table = JsonTool.GetValue(parameters, "black_flash", new Godot.Collections.Dictionary());
        foreach (var key in table.Keys)
        {
            _blackFlashPerStack[key.ToString()] = table[key].AsSingle();
        }

        // 直接写字段：改状态那句日志是给对局中看的，装配时不用响（装配日志已经够长了）
        _state = ParseState(JsonTool.GetValue(parameters, "state", _state.ToString()));
    }

    /// <summary>
    /// 装配时接线：满咒力开局。
    /// 别的组件要在面板上显示咒力，得登记"现算函数"而不是当时的值——装配期的书写顺序
    /// 不保证谁先 `Bind`（`3003` 护盾条当初就踩过这个）。
    /// </summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        _current = Amount;

        if (ball == null)
        {
            GD.PushError($"[{TypeName}] 组件没挂在球下面，咒力池自己还能用，就是日志里认不出是谁。");
        }
    }

    /// <summary>被动恢复：每秒回 `Regen`，回到上限为止。</summary>
    public override void _PhysicsProcess(double delta)
    {
        float regen = RegenNow; // 基础值 + 黑闪加成
        if (regen <= 0f || _current >= Amount)
        {
            return;
        }

        _current = Mathf.Min(Amount, _current + regen * (float)delta);
    }

    /// <summary>
    /// 花咒力：**名义消耗 × 消耗倍率**，够了就扣掉返回 true；不够就返回 false（一点不扣）。
    /// 不看熔断——术式自己看 `BurnedOut` 决定要不要停手。
    /// 成功时不打日志（可能每帧都在花），失败时打一句方便对账。
    /// </summary>
    public bool TrySpend(float cost)
    {
        if (cost <= 0f)
        {
            return true; // 不要钱的别拦
        }

        float real = cost * CostRateNow; // 基础倍率 + 黑闪加成
        if (_current < real)
        {
            // 只在"刚刚花不起"的时候喊一句：无下限那种东西是每帧来花一次的，
            // 每帧喊一句会把日志淹掉；等哪次花成功了（咒力回上来了）再把旗子放下来，
            // 下一轮干涸才会再喊一声。
            if (!_warnedEmpty)
            {
                _warnedEmpty = true;
                GD.Print($"[{TypeName}] {Who} 咒力不够：这一下要 {real:0.###}，只剩 {_current:0.###}");
            }

            return false;
        }

        _warnedEmpty = false;
        _current -= real;
        return true;
    }

    /// <summary>补咒力（反转术式、吃药之类），补到上限为止。</summary>
    public void Restore(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        _current = Mathf.Min(Amount, _current + amount);
    }

    /// <summary>
    /// 打出黑闪了：**层数 +1**（本局累加、不清零）。加成就全部自动跟着走——
    /// 池子自己那几个属性（反转术式效率、咒力恢复、消耗倍率）立刻生效，
    /// 别人的属性（伤害、护盾上限……）用 `Bonus` / `Multiplier` 来问就行。
    ///
    /// 谁打出黑闪都调这个方法。**它不动 `State`**：黑闪后是"叠起来的加成"，
    /// 不是"现在处于哪种模式"，两者混在一起会互相顶掉（类说明里写了）。
    /// </summary>
    public void EnterBlackFlash()
    {
        BlackFlashStacks++;

        GD.Print($"[{TypeName}] {Who} 打出黑闪（第 {BlackFlashStacks} 层）："
            + $"反转术式效率 {ReverseRate:0.#}、咒力恢复 {RegenNow:0.#}、消耗倍率 {CostRateNow:0.###}");
    }

    private string Who => _ball?.Name.ToString() ?? "（还没挂在球上）";

    /// <summary>
    /// 解析 Json 里的状态名：不区分大小写，下划线可有可无
    /// （`"domain_boost"` 和 `"DomainBoost"` 都认）。不认识的名字报警告并按「正常」处理。
    /// </summary>
    private static CursedEnergyState ParseState(string name)
    {
        var normalized = (name ?? string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);

        if (Enum.TryParse(normalized, true, out CursedEnergyState state))
        {
            return state;
        }

        GD.PushWarning($"[{TypeName}] 不认识的咒力状态名 '{name}'，按「正常」处理。");
        return CursedEnergyState.Normal;
    }

    /// <summary>状态的中文名，日志和面板都用它。</summary>
    public static string StateText(CursedEnergyState state) => state switch
    {
        CursedEnergyState.Normal => "正常",
        CursedEnergyState.DomainBoost => "领域中增强",
        CursedEnergyState.Burnout => "术式熔断",
        CursedEnergyState.BlackFlash => "黑闪后状态提升",
        _ => state.ToString(),
    };
}
