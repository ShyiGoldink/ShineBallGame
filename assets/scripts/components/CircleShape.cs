using Godot;

/// <summary>
/// 圆形碰撞箱：把球的碰撞圈和命中圈改成指定半径。
///
/// 不写参数就什么都不做，用预制体里的默认值（碰撞圈 75、命中圈 90）——
/// 默认值永远只留在 NormalBall.tscn 里，代码和场景不会各存一份数字。
///
/// 注意：预制体里的形状是所有球共用的同一个资源，改之前必须先复制一份，
/// 具体在 BallAssembler.SetCircleRadius 里做。
/// </summary>
public partial class CircleShape : BallComponent
{
    public override int Id => 5001;

    public override string Type => "shape.circle";

    public override string Description => "把碰撞圈 / 命中圈改成指定半径，不写就用预制体的默认圆";

    /// <summary>物理碰撞圈的半径。0 = 用预制体的值（75）。</summary>
    public float Radius;

    /// <summary>命中检测圈的半径。0 = 用预制体的值；只写了 Radius 时按 1.2 倍跟着走。</summary>
    public float HitRadius;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Radius = JsonTool.GetValue(parameters, "radius", Radius);
        HitRadius = JsonTool.GetValue(parameters, "hit_radius", HitRadius);
    }

    /// <summary>
    /// 装配时把半径写上去。两个参数都没写就什么都不做——默认圆留在预制体里。
    /// 具体怎么改（以及"改前先复制形状"那件事）都在 `BallLook` 里，碰撞圈只有那一个入口。
    /// </summary>
    public override void Bind(Ball ball, string configId)
    {
        if (ball == null || (Radius <= 0f && HitRadius <= 0f))
        {
            return;
        }

        BallLook.SetCircle(ball, Radius, HitRadius);
    }
}
