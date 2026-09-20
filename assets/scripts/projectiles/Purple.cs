using Godot;

/// <summary>
/// 虚式「茈」的那颗球：**朝一个方向直飞，撞到敌对阵营的球就造成巨额伤害，然后消失**。
///
/// 它是个"会飞的伤害包"，别的什么都不管：
/// * 不知道是谁放的、也不知道自己是怎么来的——只要 `Setup` 给的那几样（阵营、速度、伤害、留多久）；
/// * **伤害走正常的受伤链**（`TakeDamage` + `DamageEvent`），所以护盾、无敌、真伤那几环该怎么算还怎么算。
///   这就是解耦：茈球不需要知道"无下限""护盾"这些东西存在；
/// * 撞到第一个敌对球就结束（不穿透），撞自己人、撞苍/赫 都不算命中。
/// * **出来先蓄势**：`delay` 秒之内原地停着（这时候也不会撞人），蓄完后才重新索敌并朝目标冲出去；
///   `life` 是"飞多久还没撞到才消失"，**蓄势那段时间不算在里面**。
/// </summary>
public partial class Purple : Area2D
{
    /// <summary>茈球挂在这个 node group 里（结算时好一起清掉）。</summary>
    public const string PurplesGroup = "purples";

    /// <summary>预制体里那张图叫什么。</summary>
    private const string SpriteName = "Sprite";

    /// <summary>命中圈的半径比例：比本体稍大一点，免得"看着撞上了却没判定"。</summary>
    private const float HitRadiusScale = 1.1f;

    /// <summary>紫色。</summary>
    private static readonly Color PurpleColor = new Color(0.72f, 0.35f, 1f);

    private int _ownerGroup;
    private string _ownerId = string.Empty;
    private Vector2 _direction = Vector2.Right;
    private float _speed = 1000f;
    private float _damage = 1000f;
    private float _delayLeft;
    private float _lifeLeft;

    /// <summary>
    /// 放出去之前调（**进场景树之前**）：定下阵营、速度、伤害、蓄势多久、飞多久、多大。
    /// </summary>
    public void Setup(int ownerGroup, string ownerId, float speed,
        float damage, float delay, float life, float size)
    {
        _ownerGroup = ownerGroup;
        _ownerId = ownerId ?? string.Empty;
        _speed = Mathf.Max(speed, 0f);
        _damage = Mathf.Max(damage, 0f);
        _delayLeft = Mathf.Max(delay, 0f);
        _lifeLeft = Mathf.Max(life, 0f);

        AddToGroup(PurplesGroup);

        float realSize = Mathf.Max(size, 1f);
        var sprite = GetNodeOrNull<Sprite2D>(SpriteName);
        if (sprite == null)
        {
            GD.PushError($"[茈] 预制体里找不到 {SpriteName} 节点。");
        }
        else
        {
            sprite.Modulate = PurpleColor;
            BallLook.FitSprite(sprite, sprite.Texture, realSize);
        }

        // 撞人的圈：大小跟着显示尺寸走（和蛋一个道理）
        AddChild(new CollisionShape2D
        {
            Name = "Shape",
            Shape = new CircleShape2D { Radius = realSize * 0.5f * HitRadiusScale },
        });
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (delta <= 0.0)
        {
            return;
        }

        // 蓄势：刚出来先原地停一下，蓄完才冲（就是"冲击力"那一下）
        if (_delayLeft > 0f)
        {
            _delayLeft -= (float)delta;
            if (_delayLeft > 0f)
            {
                return; // 还在蓄势：不飞，也还没开始算"飞多久"
            }

            if (!TryAimAtNearestEnemy())
            {
                GD.Print($"[茈] {Name} 蓄势完毕，但没有可攻击的敌人，消失");
                QueueFree();
                return;
            }

            GD.Print($"[茈] {Name} 蓄势完毕，重新索敌后出发");
        }

        Position += _direction * (_speed * (float)delta);

        _lifeLeft -= (float)delta;
        if (_lifeLeft <= 0f)
        {
            GD.Print($"[茈] {Name} 飞到头了，消失");
            QueueFree();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_delayLeft > 0f)
        {
            return; // 还在蓄势：这会儿撞上不算命中（还没冲出去）
        }

        if (body is not Ball ball || ball.Group == _ownerGroup)
        {
            return; // 不是球、或者自己人：不算命中
        }

        GD.Print($"[茈] {Name} 命中 {ball.Name}，伤害 {_damage:0.#}");
        ball.TakeDamage(new DamageEvent(ball, _damage, "attack.hollow_purple", null, _ownerId));
        QueueFree();
    }

    private bool TryAimAtNearestEnemy()
    {
        Ball nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup(Ball.BallsGroup))
        {
            if (node is not Ball ball || ball.Group == _ownerGroup || ball.State == BallState.Dead)
            {
                continue;
            }

            float distance = GlobalPosition.DistanceSquaredTo(ball.GlobalPosition);
            if (distance < nearestDistance)
            {
                nearest = ball;
                nearestDistance = distance;
            }
        }

        if (nearest == null)
        {
            return false;
        }

        var toEnemy = nearest.GlobalPosition - GlobalPosition;
        _direction = toEnemy.LengthSquared() > 0.01f ? toEnemy.Normalized() : Vector2.Right;
        return true;
    }
}
