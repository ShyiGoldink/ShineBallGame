using Godot;

/// <summary>
/// 领域在**场上**的样子：以施术者为中心的一个圆 —— 半透明的填充 + 一圈细环，
/// 环的位置就是领域半径（默认 2px 宽，见 `RingWidth`）。
///
/// **它不是球**：不进 `balls` 组、不参与胜负、没有血量（跟蛋、苍/赫球一个待遇）。
/// 它是`Domain`（领域组件基类）开领域时造出来的**子节点**，挂在施术者身下——
/// 所以"领域跟着小球走"是天然的，不用每帧同步坐标。
///
/// 分工：
/// * **组件**管配置和账（半径、优先级、外壳、时长、什么时候开/碎）；
/// * **这里**只管画，以及回答"某个点是不是在圈里"（`Contains`，给"站在对方领域里"那套用）。
///
/// 谁在圈里用**圆心距离**判定，不做物理碰撞体：Godot 没有环形的碰撞形状，
/// 而真做成实心的话，对方球会被挡在外面、永远进不来，领域效果就永远触发不到。
/// 这一层刻意压在最下面（`z_index = -1`），不然它会盖住球和两侧面板。
/// </summary>
public partial class DomainField : Node2D
{
    /// <summary>
    /// 场上所有领域都挂这个 node group（和 `balls` / `eggs` / `orbs` 一个套路）：
    /// "现在场上有没有敌方领域""我是不是站在对方领域里"都从这个组里找。
    /// </summary>
    public const string FieldsGroup = "domains";

    /// <summary>谁展开的（阵营）。领域只对**别的阵营**生效。</summary>
    public int OwnerGroup { get; private set; }

    /// <summary>开放型领域：没有外壳，所以环画得虚一些（不吃伤害、也不参与优先级比较）。</summary>
    public bool Open;

    /// <summary>优先级：范围互相压缩时比这个数（开放型不参与）。</summary>
    public int Priority = 10;

    /// <summary>**当前有效半径**（每帧由领域组件推过来）。被更强的领域压缩时它就变小。</summary>
    public float Radius = 360f;

    /// <summary>外壳还剩多少（1 = 满、0 = 碎）。开放型没有外壳，固定是 1。</summary>
    public float ShellRatio = 1f;

    /// <summary>这会儿算不算"生效中"。碎掉的领域立刻置 false，判定就不再看它了。</summary>
    public bool Active = true;

    /// <summary>填充色（半透明）。子类可以改成自己的配色。</summary>
    public Color FillColor = new Color(0.12f, 0.12f, 0.18f, 0.32f);

    /// <summary>环的颜色。</summary>
    public Color RingColor = new Color(0.85f, 0.85f, 1f, 0.9f);

    /// <summary>环宽（像素）。</summary>
    public float RingWidth = 2f;

    /// <summary>展开用多久（秒）：从球心撑到半径。</summary>
    private const float GrowTime = 0.35f;

    /// <summary>碎掉用多久（秒）：环亮一下散开，然后自己消失。</summary>
    private const float ShatterTime = 0.3f;

    /// <summary>展开进度（0~1）。</summary>
    private float _grow;

    /// <summary>碎裂进度：小于 0 = 没在碎。</summary>
    private float _shatter = -1f;

    /// <summary>
    /// 展开之前调（组件那边）：定下阵营、半径、是不是开放型，并把自己挂进 `domains` 组。
    /// 视觉参数（配色、环宽）在挂进场景树之前赋值即可。
    /// </summary>
    public void Setup(int ownerGroup, float radius, bool open)
    {
        OwnerGroup = ownerGroup;
        Radius = Mathf.Max(radius, 0f);
        Open = open;
        Active = true;

        ZIndex = -1; // 压在所有球下面
        AddToGroup(FieldsGroup);
    }

    /// <summary>
    /// 这个点是不是在圈里（用全局坐标比，所以施术者跑到哪圈就在哪）。
    /// "站在对方领域里"就是拿球心问这个。
    /// </summary>
    public bool Contains(Vector2 point) => Active && GlobalPosition.DistanceTo(point) <= Radius;

    /// <summary>
    /// 碎掉：从这一刻起不再生效（`Active = false`，判定立刻不认它），
    /// 环亮一下散开、然后自己 `QueueFree`。和蛋那边"收壳"是同一个位置、同一个道理。
    /// </summary>
    public void Shatter()
    {
        if (_shatter >= 0f)
        {
            return; // 已经在碎了
        }

        Active = false;
        _shatter = 0f;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        bool dirty = false;

        // 展开
        if (_grow < 1f)
        {
            _grow = Mathf.Min(1f, _grow + (float)delta / GrowTime);
            dirty = true;
        }

        // 碎裂
        if (_shatter >= 0f)
        {
            _shatter += (float)delta;
            dirty = true;

            if (_shatter >= ShatterTime)
            {
                QueueFree();
                return;
            }
        }

        // 圈里的东西一直在动（无量空处那种漂浮的点），所以生效期间每一帧都要重画；
        // 碎掉/刚展开那几帧也照样要重画（那时候 Active 已经关掉了，靠 dirty 兜住）
        if (Active || dirty)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        // 展开是"先快后慢"的（平滑一下，不然圈是匀速撑开的，看着像充气）
        float ease = _grow * _grow * (3f - 2f * _grow);
        float radius = Radius * ease;
        float fade = 1f;
        bool shattering = _shatter >= 0f;

        if (shattering)
        {
            // 碎：环往外炸一点、整体淡出，填充收得比环快（壳没了，里面就空了）
            float t = _shatter / ShatterTime;
            radius *= 1f + 0.15f * t;
            fade = 1f - t;
        }

        var fill = FillColor;
        fill.A *= fade * (Open ? 0.6f : 1f);

        // 填充：用一圈同心圆叠出来，边缘才不会是一刀切的硬边
        DrawCircle(Vector2.Zero, radius, fill);

        if (shattering)
        {
            return; // 碎了就不画细节了
        }

        // 圈里的细节（子类加：无量空处那种漂的白点）
        DrawInside(radius);

        // 环：外壳还在就亮，越接近碎越暗；开放型没有壳，画得虚一些
        var ring = RingColor;
        ring.A *= Open ? 0.45f : Mathf.Lerp(0.35f, 1f, ShellRatio);

        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 128, ring, RingWidth, true);

        if (!Open && ShellRatio > 0f)
        {
            // 外壳血自己也在环上走一圈：还剩多少，就亮多少圈（一眼看出壳厚不厚）
            var bright = RingColor;
            bright.A *= 0.85f;
            DrawArc(Vector2.Zero, radius, -Mathf.Pi * 0.5f,
                -Mathf.Pi * 0.5f + Mathf.Tau * Mathf.Clamp(ShellRatio, 0f, 1f),
                128, bright, RingWidth, true);
        }
    }

    /// <summary>
    /// 圈里的装饰，给子类覆盖（默认什么都不画）。
    /// `radius` 是当前画出来的半径，按它铺东西就行——展开和碎裂都会自动跟着走。
    /// </summary>
    protected virtual void DrawInside(float radius)
    {
    }

}
