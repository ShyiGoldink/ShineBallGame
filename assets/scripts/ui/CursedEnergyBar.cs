using Godot;

/// <summary>
/// 咒力条：球身上那根紫色细条，显示咒力还剩多少。
///
/// **位置和显隐不在这里**——它由条区（`BallLayout`）排在球下面，只有挂了咒力条的组件
/// （`7002`）才造得出它。这里只管自己的样子：底色、紫色填充、圆角，以及自己的尺寸
/// （尺寸是"条自己带"的那一份，布局照着它排）。
/// </summary>
public partial class CursedEnergyBar : ProgressBar
{
    /// <summary>咒力的颜色。用紫色，跟血条（白/黄/橘/红）和护盾条（青）都分得开。</summary>
    private static readonly Color EnergyColor = new Color(0.62f, 0.42f, 1f);

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
            BgColor = EnergyColor,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        AddThemeStyleboxOverride("background", background);
        AddThemeStyleboxOverride("fill", _fill);
    }

    /// <summary>
    /// 按当前咒力和上限刷新长度。面板上的数字由组件登记的那行读数负责，
    /// 这条只管长短。
    /// </summary>
    public void SetEnergy(float current, float max)
    {
        MaxValue = max > 0f ? max : 1f;
        Value = Mathf.Clamp(current, 0f, MaxValue);
    }
}
