using Godot;

/// <summary>
/// 斩击刻印（`7003`）：宿儺的**专属资源池**——每一次斩击落下去就刻一格，
/// 刻得越多，**灶開**（`2009`）那一下越狠。
///
/// 它和咒力池（`7001`）是同一个定位：**全项目只有这一份数字**，
/// 加格的（解 `2007`、捌 `2008`、领域的必中斩 `8004`）和读数的（灶開）都不自己存一份，
/// 都来找它。所以想改"刻印怎么涨、怎么掉、有没有上限"，只动这一个文件。
///
/// 接口只有三个：`Add(几格)` 加、`Consume()` 清空（灶開放完用）、`Count` 读。
/// 面板上自动多一行"斩击刻印 N"（登记的是现算函数，不会过期）。
/// </summary>
public partial class SlashMark : BallComponent
{
    /// <summary>类型名。"按 Type 找兄弟"和 `Requirements` 都用这个字符串。</summary>
    public const string TypeName = "jujutsu.slash_mark";

    public override int Id => 7003;

    public override string Type => TypeName;

    public override string DisplayName => "斩击刻印";

    public override string Description => "每斩中一下就刻一格，刻得越多灶开的伤害越高（Add / Consume / Count）";

    /// <summary>上限。0 = 不封顶。</summary>
    public int Max;

    /// <summary>每秒自动掉几格。0 = 不掉（默认：攒起来就一直留着）。</summary>
    public float Decay;

    private Ball _ball;
    private float _decayLeft;

    /// <summary>现在刻了几格。</summary>
    public int Count { get; private set; }

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Max = JsonTool.GetValue(parameters, "max", Max);
        Decay = JsonTool.GetValue(parameters, "decay", Decay);
    }

    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，刻印记不了。");
            return;
        }

        // 面板那一行：登记现算函数，别人加格、清空都自动跟着变
        ball.Readouts.Add(new BallReadout(DisplayName, Readout));

        GD.Print($"[{Type}] {ball.Name} 的{DisplayName}就绪"
            + (Max > 0 ? $"（上限 {Max} 格）" : "（不封顶）")
            + (Decay > 0f ? $"，每秒掉 {Decay:0.#} 格" : string.Empty));
    }

    /// <summary>刻几格（斩击命中时由攻击组件调）。返回刻完之后的格数。</summary>
    public int Add(int marks = 1)
    {
        if (marks <= 0)
        {
            return Count;
        }

        Count += marks;
        if (Max > 0 && Count > Max)
        {
            Count = Max;
        }

        return Count;
    }

    /// <summary>清空（灶開放完那一下用）。返回清掉之前有几格。</summary>
    public int Consume()
    {
        int used = Count;
        Count = 0;
        _decayLeft = 0f;
        return used;
    }

    /// <summary>会掉的话每秒往下掉一格一格（默认不掉）。</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (Decay <= 0f || Count <= 0)
        {
            return;
        }

        _decayLeft += Decay * (float)delta;
        while (_decayLeft >= 1f && Count > 0)
        {
            Count--;
            _decayLeft -= 1f;
        }
    }

    private string Readout() => Max > 0 ? $"{Count} / {Max}" : $"{Count}";

    /// <summary>按 `Type` 名在球身上找刻印池（域外伤害要记在领域持有者头上时用它）。</summary>
    public static SlashMark Find(Ball ball)
    {
        if (ball == null)
        {
            return null;
        }

        foreach (var child in ball.GetChildren())
        {
            if (child is SlashMark mark)
            {
                return mark;
            }
        }

        return null;
    }
}
