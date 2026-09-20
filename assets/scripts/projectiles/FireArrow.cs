using Godot;

/// <summary>
/// **灶開的火焰**（`2009` 放出来的）：一团橙红的火朝目标飞，命中就炸。
///
/// 基类 `DamageProjectile` 管飞行/判定/计时；这里只多一件事：**炸的范围**
/// （`splash` 半径内的敌对球也各挨一下，0 = 只打命中的那一个，不炸）。
/// 伤害是放它的组件（`2009`）按斩击刻印算好的——这里不认识刻印。
/// </summary>
public partial class FireArrow : DamageProjectile
{
    /// <summary>命中时的火色。</summary>
    private static readonly Color FireColor = new Color(1f, 0.52f, 0.16f, 0.95f);

    private static readonly Color FlameColor = new Color(1f, 0.82f, 0.35f, 0.55f);

    /// <summary>炸开的半径（像素）。0 = 只打命中的那一个。</summary>
    private float _splash;

    /// <summary>放出来之前调（进树之前）：伤害、来源、方向、速度、留场、大小、炸多大。</summary>
    public void Launch(Ball caster, float damage, string sourceType, Vector2 direction,
        float speed, float life, float size, float splash)
    {
        _splash = Mathf.Max(splash, 0f);

        Setup(caster, caster == null ? 0 : caster.Group, caster?.Id, damage, sourceType,
            direction, speed, 0f, life, size, FireColor);
    }

    /// <summary>命中：如果配了炸的范围，把范围内的敌对球也各打一下（命中的那个已经挨过了）。</summary>
    protected override void OnHitBall(Ball ball)
    {
        if (_splash <= 0f)
        {
            return;
        }

        GD.Print($"[{SourceType}] {Name} 炸开：半径 {_splash:0}");

        foreach (var node in GetTree().GetNodesInGroup(Ball.BallsGroup))
        {
            if (node is not Ball other || other == ball
                || other.Group == OwnerGroup || other.State == BallState.Dead)
            {
                continue;
            }

            if (GlobalPosition.DistanceTo(other.GlobalPosition) <= _splash)
            {
                other.TakeDamage(new DamageEvent(other, Damage, SourceType, null, OwnerId));
            }
        }
    }

    /// <summary>一团火：外面一圈淡的、里面一颗亮的。</summary>
    protected override void DrawShape()
    {
        DrawCircle(Vector2.Zero, Size * 0.5f, FlameColor);
        DrawCircle(Vector2.Zero, Size * 0.32f, BodyColor);
        DrawCircle(Vector2.Zero, Size * 0.16f, new Color(1f, 0.95f, 0.75f, 1f));
    }
}
