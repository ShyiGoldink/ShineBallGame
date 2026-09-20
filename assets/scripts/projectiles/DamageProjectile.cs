using Godot;
using System.Collections.Generic;

/// <summary>
/// **会飞的伤害包**（茈、解/捌的斩击、灶開的火焰都是它）：朝一个方向直飞，撞到敌对阵营的球就算命中，
/// 伤害走**正常的受伤链**（`TakeDamage` + `DamageEvent`），然后自己消失。
///
/// 抽这一层的原因：三种飞行物**只有长相和"命中之后多做一件什么"不同**，飞行、判定、留场计时、
/// 阵营判断、撞自己人不算命中——全都一样。所以基类管那一套，子类只管两件事：
/// `DrawShape()`（画成什么样）和 `OnHitBall()`（命中之后除了伤害还干什么，比如给主人刻一格）。
///
/// 它不认识的：谁放的（只知道阵营和 id）、伤害怎么算出来的（`Setup` 给多少就是多少）、
/// 对面的护盾/无敌/无下限怎么算（那是受伤链的事）。所以"新的飞行物"= 再写一个小类。
///
/// 图全是代码画的（不占素材、不用过导入）；判定圈是进树之前按 `Size` 生成的。
/// </summary>
public partial class DamageProjectile : Area2D
{
    /// <summary>场上所有飞行物都挂这个 node group（结算时一起清掉）。</summary>
    public const string ProjectilesGroup = "projectiles";

    /// <summary>同类飞行物的编号（给节点起名用，见 `Numbered`）。</summary>
    private static readonly Dictionary<string, int> SerialByBase = new();

    /// <summary>命中圈的半径比例：比本体稍大一点，免得"看着撞上了却没判定"。</summary>
    protected const float HitRadiusScale = 1.1f;

    /// <summary>
    /// 放它出来的那颗球（可以没有：环境伤害、以后的陷阱）。给"命中之后记在谁头上"用。
    /// 名字叫 `Caster` 不叫 `Owner`——`Node.Owner` 是引擎自己的字段，别撞名。
    /// </summary>
    protected Ball Caster;

    /// <summary>谁放的（阵营）。打不中自己人。</summary>
    protected int OwnerGroup { get; private set; }

    /// <summary>来源的球 id（伤害事件里记的"谁的账"）。</summary>
    protected string OwnerId { get; private set; } = string.Empty;

    /// <summary>这一下打多少（由放它的组件在 `Setup` 里算好）。</summary>
    protected float Damage { get; private set; } = 1000f;

    /// <summary>伤害事件里的"哪种攻击"（`attack.xxx` / `domain.xxx`），来源识别用。</summary>
    protected string SourceType = "attack.projectile";

    /// <summary>飞多快（像素/秒）。</summary>
    protected float Speed { get; private set; } = 1000f;

    /// <summary>出来先原地蓄势多久（秒）。这段时间不飞、也不算"飞多久"，也还不吃碰撞。</summary>
    protected float DelayLeft { get; private set; }

    /// <summary>飞多久还没撞到人就消失（秒）。蓄势那段时间不算在里面。</summary>
    protected float LifeLeft { get; private set; } = 3f;

    /// <summary>多大（像素，直径）。</summary>
    protected float Size { get; private set; } = 64f;

    /// <summary>画出来的颜色。</summary>
    protected Color BodyColor = new Color(1f, 1f, 1f, 0.9f);

    /// <summary>飞行方向（单位向量）。</summary>
    protected Vector2 Direction = Vector2.Right;

    /// <summary>这会儿是不是还在蓄势。</summary>
    protected bool Charging => DelayLeft > 0f;

    /// <summary>
    /// 放出去之前调（**进场景树之前**，和茈/苍赫一个位置）：
    /// 定下阵营、伤害、来源、方向、速度、蓄势、留场、大小、颜色。
    /// </summary>
    public void Setup(Ball caster, int ownerGroup, string ownerId, float damage, string sourceType,
        Vector2 direction, float speed, float delay, float life, float size, Color color)
    {
        Caster = caster;
        OwnerGroup = ownerGroup;
        OwnerId = ownerId ?? string.Empty;
        Damage = Mathf.Max(damage, 0f);
        SourceType = sourceType ?? SourceType;
        Direction = direction.LengthSquared() > 0.001f ? direction.Normalized() : Vector2.Right;
        Speed = Mathf.Max(speed, 0f);
        DelayLeft = Mathf.Max(delay, 0f);
        LifeLeft = Mathf.Max(life, 0f);
        Size = Mathf.Max(size, 1f);
        BodyColor = color;

        AddToGroup(ProjectilesGroup);

        // 命中圈：大小跟着显示尺寸走（和蛋、茈一个道理）
        AddChild(new CollisionShape2D
        {
            Name = "Shape",
            Shape = new CircleShape2D { Radius = Size * 0.5f * HitRadiusScale },
        });
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        QueueRedraw(); // 子类可能画会动的东西
    }

    public override void _PhysicsProcess(double delta)
    {
        if (delta <= 0.0)
        {
            return;
        }

        // 蓄势：原地停着，蓄完再交给子类决定往哪飞
        if (DelayLeft > 0f)
        {
            DelayLeft -= (float)delta;
            if (DelayLeft > 0f)
            {
                return;
            }

            OnChargeFinished();
        }

        Position += Direction * (Speed * (float)delta);

        LifeLeft -= (float)delta;
        if (LifeLeft <= 0f)
        {
            GD.Print($"[{SourceType}] {Name} 飞到头了，消失");
            QueueFree();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (Charging)
        {
            return; // 还在蓄势：这会儿撞上不算命中（还没冲出去）
        }

        if (body is not Ball ball || ball.Group == OwnerGroup)
        {
            return; // 不是球、或者自己人：不算命中
        }

        ball.TakeDamage(new DamageEvent(ball, Damage, SourceType, null, OwnerId));
        OnHitBall(ball);

        QueueFree();
    }

    public override void _Draw()
    {
        if (Charging)
        {
            return; // 蓄势期间不画东西（子类想画就覆盖）
        }

        DrawShape();
    }

    /// <summary>
    /// 画这一发长什么样（局部坐标，原点就是它的中心）。默认画一个圆，
    /// 子类想画成别的形状（比如一道细长的斩击）就覆盖它。
    /// </summary>
    protected virtual void DrawShape()
    {
        DrawCircle(Vector2.Zero, Size * 0.5f, BodyColor);
    }

    /// <summary>蓄势结束的那一刻（默认什么都不做；要重新索敌的子类在这里改 `Direction`）。</summary>
    protected virtual void OnChargeFinished()
    {
    }

    /// <summary>
    /// 命中之后（伤害已经打进去了）多做一件什么。
    /// 默认什么都不做；要"顺手给主人刻一格"的子类覆盖它（见 `SlashProjectile`）。
    /// </summary>
    protected virtual void OnHitBall(Ball ball)
    {
    }

    /// <summary>朝场上最近的敌对球转向（蓄势结束时用）。没有敌人就返回 false。</summary>
    protected bool AimAtNearestEnemy()
    {
        var nearest = NearestEnemyOf(Caster, OwnerGroup, GlobalPosition);
        if (nearest == null)
        {
            return false;
        }

        var to = nearest.GlobalPosition - GlobalPosition;
        Direction = to.LengthSquared() > 0.01f ? to.Normalized() : Direction;
        return true;
    }

    /// <summary>
    /// 离某个点最近的敌对球。放飞行物的组件（斩击、灶開）也用它来算"朝谁打/打谁"——
    /// 所以"怎么挑目标"只有这一份实现，想改成"优先打血最少的"只改这儿。
    /// `self` 是放的人（可以没有），`group` 是"谁的阵营不算敌人"。
    /// </summary>
    public static Ball NearestEnemyOf(Ball self, int group, Vector2 from)
    {
        if (self == null || self.GetTree() == null)
        {
            return null;
        }

        Ball nearest = null;
        float best = float.MaxValue;

        foreach (var node in self.GetTree().GetNodesInGroup(Ball.BallsGroup))
        {
            if (node is not Ball ball || ball.Group == group || ball.State == BallState.Dead)
            {
                continue;
            }

            float distance = from.DistanceSquaredTo(ball.GlobalPosition);
            if (distance < best)
            {
                nearest = ball;
                best = distance;
            }
        }

        return nearest;
    }

    /// <summary>
    /// **往敌人"将要到的地方"打的提前量**：目标在飞，直着打过去多半会落空。
    /// 按"走到目标现在这个位置要多久"估一下它那时候在哪，朝那儿飞
    /// （一阶估算，够用了；想要纯直线瞄准就直接拿"对方位置 − 自己位置"，别调这个）。
    /// </summary>
    public static Vector2 LeadDirection(Vector2 from, Ball target, float speed)
    {
        if (target == null)
        {
            return Vector2.Right;
        }

        var to = target.GlobalPosition - from;
        float travel = speed > 1f ? to.Length() / speed : 0f;
        var predicted = target.GlobalPosition + target.Velocity * travel;
        var aim = predicted - from;

        return aim.LengthSquared() > 0.01f ? aim.Normalized() : to.Normalized();
    }

    /// <summary>
    /// 给同一种飞行物起带序号的名字（`斩击·解#1`、`斩击·解#2`……）。
    /// 不起序号的话，同一帧放出去的两发同名，引擎会把后面那发改成 `@Area2D@19` 那种，日志没法看。
    /// </summary>
    public static string Numbered(string baseName)
    {
        SerialByBase.TryGetValue(baseName, out int serial);
        serial++;
        SerialByBase[baseName] = serial;
        return $"{baseName}#{serial}";
    }
}
