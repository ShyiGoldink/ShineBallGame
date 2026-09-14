using Godot;

/// <summary>
/// 外观层：球"长什么样、多大、判定圈多大"**只从这里走**。
///
/// 三种外观来源——`type = 1` 的贴图球、`type = 2` 的 Spine 球、蛋——都调这里的函数，
/// 所以尺寸规则和碰撞圈只有一份实现，改规则只改这个文件。
///
/// 尺寸怎么定：`DisplaySize` 是球在场上的目标大小（像素，按素材长边算）。
/// 贴图和骨架都缩进这个尺寸，而**碰撞圈不在这里写死**——半径由预制体或 `5001`
/// 决定，`PlaceHealthBar` 再按最终半径把血条摆好。
/// </summary>
public static class BallLook
{
    /// <summary>球在画面上的统一显示尺寸（像素，按素材长边算）。</summary>
    public const float DisplaySize = 100f;

    /// <summary>命中圈 / 碰撞圈的默认比例。只给了碰撞圈时，命中圈按这个比例跟上。</summary>
    public const float HitRadiusScale = 1.2f;

    private const string BodyShapePath = "Shape";
    private const string HitShapePath = "HitArea/Shape";

    /// <summary>
    /// 按数据摆好外观：`type = 2` 走 Spine，其它走贴图。
    /// Spine 缺东西（没资源、扩展没加载）会自动回落贴图，不会留下一个黑球。
    /// </summary>
    public static void Apply(Ball ball, BallData data)
    {
        if (data.Type == 2 && SpineLook.Attach(ball, data.SpineScale))
        {
            return;
        }

        ApplyTexture(ball, data.Id);
    }

    /// <summary>
    /// **改碰撞圈的唯一入口**：半径必填，命中圈不写就按 `HitRadiusScale` 跟上。
    /// 预制体里两个圈是共用的资源，所以复制那份逻辑也在这里（见 `SetCircleRadius`）。
    /// </summary>
    public static void SetCircle(Ball ball, float radius, float hitRadius = 0f)
    {
        if (ball == null)
        {
            return;
        }

        if (radius > 0f)
        {
            SetCircleRadius(ball, BodyShapePath, radius, "碰撞圈");

            if (hitRadius <= 0f)
            {
                hitRadius = radius * HitRadiusScale; // 只给了碰撞圈：命中圈按比例跟上
            }
        }

        if (hitRadius > 0f)
        {
            SetCircleRadius(ball, HitShapePath, hitRadius, "命中圈");
        }
    }

    /// <summary>
    /// 把一张图按长边缩到指定像素（球和蛋共用，所以场上所有东西是同一套比例）。
    /// 素材画多大都行——512 也好 2048 也好，缩完长得一样大。
    /// </summary>
    public static void FitSprite(Sprite2D sprite, Texture2D texture, float size)
    {
        if (sprite == null || texture == null || size <= 0f)
        {
            return;
        }

        float longest = Mathf.Max(texture.GetWidth(), texture.GetHeight());
        sprite.Scale = Vector2.One * (size / longest);
    }

    /// <summary>
    /// 摆血条：**大小固定**（所有球都是 160×14），只有位置跟着球走——
    /// 挂在球底下方 10 像素，所以 100px 和 150px 的球都不会被自己的血条盖住。
    /// </summary>
    public static void PlaceHealthBar(Ball ball)
    {
        var bar = ball?.GetNodeOrNull<ProgressBar>("HealthBar");
        if (bar == null)
        {
            return;
        }

        float radius = 50f; // 拿不到形状时的兜底
        if (ball.GetNodeOrNull<CollisionShape2D>(BodyShapePath)?.Shape is CircleShape2D circle)
        {
            radius = circle.Radius;
        }

        const float halfWidth = 80f; // 宽 160，固定
        const float gap = 10f;
        const float height = 14f;

        bar.OffsetLeft = -halfWidth;
        bar.OffsetRight = halfWidth;
        bar.OffsetTop = radius + gap;
        bar.OffsetBottom = radius + gap + height;
    }

    /// <summary>`type = 1`：用球自己目录里的 `resource/avatar.png`，缩到统一尺寸。</summary>
    private static void ApplyTexture(Ball ball, string ballId)
    {
        var body = ball.GetNodeOrNull<Sprite2D>("Body");
        if (body == null)
        {
            GD.PushError("[外观] 预制体里找不到 Body 节点，贴图没换。");
            return;
        }

        var texture = BallLibrary.LoadAvatar(UserData.Balls + ballId + "/resource/avatar.png");
        if (texture == null)
        {
            return; // 没有头像就用预制体里那张默认贴图
        }

        body.Texture = texture;
        FitSprite(body, texture, DisplaySize);
    }

    /// <summary>
    /// 改一个碰撞圈的半径。
    ///
    /// 改之前**必须先复制**：预制体里那个 `CircleShape2D` 是所有球共用的同一个资源
    /// （`resource_local_to_scene = false`），直接改半径会让场上一屏的球、乃至之后每个新球一起变。
    /// </summary>
    private static void SetCircleRadius(Ball ball, string shapePath, float radius, string label)
    {
        var node = ball.GetNodeOrNull<CollisionShape2D>(shapePath);
        if (node == null)
        {
            GD.PushError($"[外观] 预制体里找不到{label}节点（{shapePath}），半径没改成功。");
            return;
        }

        if (node.Shape is not CircleShape2D circle)
        {
            GD.PushError($"[外观] {label}不是圆形，改不了它。");
            return;
        }

        circle = (CircleShape2D)circle.Duplicate(); // 复制成这颗球自己的形状
        circle.Radius = radius;
        node.Shape = circle;

        GD.Print($"[外观] {ball.Name} 的{label}半径改成 {radius:0.#}");
    }
}
