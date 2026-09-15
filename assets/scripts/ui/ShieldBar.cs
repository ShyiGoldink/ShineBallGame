using Godot;

/// <summary>
/// 护盾条：球身上那根细条（在血条正下方），显示护盾还剩多少。
///
/// **显隐和位置不在这里**——显隐由 `3003 defense.shield_gauge` 通过外观层
/// （`BallLook.ShowShieldBar`）打开，位置由条区 `BallLayout` 排（血条下面挨着排），
/// 所以没护盾的球看不到它、有护盾的球也不会和别的条叠在一起。
/// 这里只管自己的样子：底色、青色填充、圆角。
/// </summary>
public partial class ShieldBar : ProgressBar
{
    /// <summary>护盾的颜色。用青色，跟血条（白 / 黄 / 橘 / 红）分开——一眼能认出这是盾不是血。</summary>
    private static readonly Color ShieldColor = new Color(0.35f, 0.8f, 1f);

    private StyleBoxFlat _fill;

    public override void _Ready()
    {
        ShowPercentage = false;

        // 大小固定（跟血条一样宽、细一点）；位置由条区 BallLayout 排
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
            BgColor = ShieldColor,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        AddThemeStyleboxOverride("background", background);
        AddThemeStyleboxOverride("fill", _fill);
    }

    /// <summary>
    /// 按当前护盾和上限刷新长度。盾破了（0）也照常显示一条空条——
    /// 空着才看得出它在慢慢回，直接藏掉反而像坏了。
    /// </summary>
    public void SetShield(float current, float max)
    {
        MaxValue = max > 0f ? max : 1f;
        Value = Mathf.Clamp(current, 0f, MaxValue);
    }
}
