using Godot;

/// <summary>
/// 场上的苍球 / 赫球（**苍和赫共用一个预制体**，靠 `Kind` 分：苍蓝、赫红）。
///
/// 一个的生命周期：
/// 1. **飞**：从放出它的那颗球的位置出发，沿着方向飞 `distance` 像素就停（速度 `speed`）；
/// 2. **停下就开始影响场上**：到位那一刻把 `Active` 打开，牵引组件（`1002`）从这一帧起才理它；
/// 3. **留 `life` 秒**（默认 30）然后自己消失（`QueueFree`，和蛋一个收摊方式）。
///
/// 它**不是球**：不进 `balls` 组、不参与胜负、没有血量（详见底类 `JujutsuOrb`）。
/// 现在用的是占位图（`Orb.png` 一张白球，靠 `Modulate` 染成蓝/红），等美术给素材就换掉。
/// </summary>
public partial class Orb : JujutsuOrb
{
    /// <summary>预制体里那张图叫什么。</summary>
    private const string SpriteName = "Sprite";

    /// <summary>苍的颜色（蓝）。</summary>
    private static readonly Color AttractColor = new Color(0.38f, 0.62f, 1f);

    /// <summary>赫的颜色（红）。</summary>
    private static readonly Color RepelColor = new Color(1f, 0.36f, 0.32f);

    /// <summary>场上占多大（像素，按素材长边算）。</summary>
    private const float Size = 64f;

    /// <summary>
    /// 撞墙检测用的形状半径：**把球身也算进去**，苍/赫 停在离墙这么远的地方，
    /// 免得"球被吸到贴着墙、速度还在、看着像卡住"。
    /// </summary>
    private const float WallGap = 60f;

    private Vector2 _direction = Vector2.One.Normalized();
    private float _speed;
    private float _distanceLeft;
    private float _lifeLeft;
    private ShapeCast2D _wallCast;

    /// <summary>
    /// 放出去之前调（**进场景树之前**，和蛋的 `SetOwner` 同一个位置）：
    /// 定下是苍还是赫、往哪飞、飞多远、多快、留多久。
    /// </summary>
    public void Setup(OrbKind kind, Vector2 direction, float distance, float speed, float life)
    {
        Kind = kind;
        _direction = direction.LengthSquared() > 0.001f ? direction.Normalized() : Vector2.One.Normalized();
        _speed = Mathf.Max(speed, 0f);
        _distanceLeft = Mathf.Max(distance, 0f);
        _lifeLeft = Mathf.Max(life, 0f);

        Active = _distanceLeft <= 0f; // 不用飞就直接算到位

        var sprite = GetNodeOrNull<Sprite2D>(SpriteName);
        if (sprite == null)
        {
            GD.PushError($"[苍/赫] 预制体里找不到 {SpriteName} 节点，看不出是苍还是赫。");
            return;
        }

        sprite.Modulate = kind == OrbKind.Attract ? AttractColor : RepelColor;
        BallLook.FitSprite(sprite, sprite.Texture, Size);

        // 往前探路的"触角"：撞到不会动的东西（墙）就停下（见 BlockedAhead）
        _wallCast = new ShapeCast2D
        {
            Name = "WallCast",
            Shape = new CircleShape2D { Radius = WallGap },
            CollisionMask = 1,
            Enabled = true,
        };
        AddChild(_wallCast);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (delta <= 0.0)
        {
            return;
        }

        // 1. 还在飞：往前推一步
        if (_distanceLeft > 0f)
        {
            float step = Mathf.Min(_speed * (float)delta, _distanceLeft);

            if (BlockedAhead(step))
            {
                Arrive("撞到墙，停在场地里");
            }
            else
            {
                Position += _direction * step;
                _distanceLeft -= step;

                if (_distanceLeft <= 0f)
                {
                    Arrive("到位");
                }
            }
        }

        // 2. 留场计时：飞的时间也算在里面（从放出来那一刻开始数）
        _lifeLeft -= (float)delta;
        if (_lifeLeft <= 0f)
        {
            GD.Print($"[苍/赫] {Name} 留场时间到，消失");
            QueueFree();
        }
    }

    /// <summary>到位：从这一帧起开始影响场上的球，也就不再飞了。</summary>
    private void Arrive(string why)
    {
        _distanceLeft = 0f;
        Active = true;
        GD.Print($"[苍/赫] {Name} {why}，开始影响场上（{Kind}）");
    }

    /// <summary>
    /// 往前飞这一步会不会撞上"不会动的东西"（墙）。
    /// **球不算**——苍/赫 是从球身上飞过去的，只有墙体建筑才拦它。
    /// </summary>
    private bool BlockedAhead(float step)
    {
        if (_wallCast == null)
        {
            return false;
        }

        _wallCast.TargetPosition = _direction * step;
        _wallCast.ForceShapecastUpdate();

        for (int i = 0; i < _wallCast.GetCollisionCount(); i++)
        {
            if (_wallCast.GetCollider(i) is StaticBody2D)
            {
                return true;
            }
        }

        return false;
    }
}
