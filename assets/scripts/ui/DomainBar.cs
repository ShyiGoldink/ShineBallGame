using Godot;

/// <summary>
/// 领域条：球身上那根显示**领域外壳还剩多少**的细条（开放型领域没有壳，显示的是剩余时间）。
///
/// 和咒力条 / 护盾条完全一个规格：**位置和显隐不在这里**——条是领域组件
/// （`Domain`）在装配时造出来挂进条区（`BallLayout`）的，没开领域的时候它是隐藏的、
/// 不占地方；排到哪儿、什么时候排，全归条区管。这里只管自己的样子：底色、淡紫填充、圆角。
/// </summary>
public partial class DomainBar : ProgressBar
{
    /// <summary>领域条的颜色。用淡紫白，跟血条（白/黄/橘/红）、护盾条（青）、咒力条（紫）都分得开。</summary>
    private static readonly Color DomainColor = new Color(0.88f, 0.86f, 1f);

    private StyleBoxFlat _fill;

    public override void _Ready()
    {
        ShowPercentage = false;

        // 尺寸是这条自己的事：宽度和别的条一致（160），高度跟护盾条一样细
        CustomMinimumSize = new Vector2(160f, 8f);

        var background = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.1f, 0.85f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        _fill = new StyleBoxFlat
        {
            BgColor = DomainColor,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        AddThemeStyleboxOverride("background", background);
        AddThemeStyleboxOverride("fill", _fill);
    }

    /// <summary>按"还剩百分之多少"刷新长度（外壳比例，或者开放型的剩余时间比例）。</summary>
    public void SetRatio(float ratio)
    {
        MaxValue = 1f;
        Value = Mathf.Clamp(ratio, 0f, 1f);
    }
}
