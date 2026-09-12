using Godot;

/// <summary>
/// 血条：一个圆角的细长长方形，只按剩余百分比换颜色，不显示数字。
/// 满血白色；不满血黄色；剩 50% 及以下橘色；剩 25% 及以下红色。
/// 数值由小球在改血量时推进来（Ball.RefreshHealthBar），自己不用盯着血量。
/// </summary>
public partial class HealthBar : ProgressBar
{
    private StyleBoxFlat _fill;

    public override void _Ready()
    {
        ShowPercentage = false;
        CustomMinimumSize = new Vector2(160f, 14f);

        var background = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.1f, 0.85f),
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
        };

        _fill = new StyleBoxFlat
        {
            BgColor = Colors.White,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
        };

        AddThemeStyleboxOverride("background", background);
        AddThemeStyleboxOverride("fill", _fill);

        // 装配时可能先设了血量、这时节点还没进场景树，颜色在这里补上
        _fill.BgColor = ColorForRatio((float)(Value / MaxValue));
    }

    /// <summary>按当前血量和上限刷新长度与颜色。</summary>
    public void SetHealth(float current, float max)
    {
        MaxValue = max > 0f ? max : 1f;
        Value = Mathf.Clamp(current, 0f, MaxValue);

        if (_fill == null)
        {
            return; // 节点还没 _Ready
        }

        _fill.BgColor = ColorForRatio((float)(Value / MaxValue));
    }

    private static Color ColorForRatio(float ratio)
    {
        if (ratio >= 1f)
        {
            return Colors.White;
        }

        if (ratio > 0.5f)
        {
            return new Color(1f, 0.9f, 0.25f); // 黄
        }

        if (ratio > 0.25f)
        {
            return new Color(1f, 0.55f, 0.15f); // 橘
        }

        return new Color(1f, 0.25f, 0.22f); // 红
    }
}
