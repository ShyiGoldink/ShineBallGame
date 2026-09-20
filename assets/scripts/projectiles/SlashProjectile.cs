using Godot;

/// <summary>
/// **斩击**（宿儺的「解」`2007` 和「捌」`2008` 放的就是它）：一道细长的斩痕朝目标直飞，
/// 命中第一个敌对球 → 伤害走正常受伤链 → **给施术者刻一格斩击刻印**（`7003`），然后消失。
///
/// 基类 `DamageProjectile` 管飞行/判定/计时，这里只做三件自己的事：
/// 画成一道细长的弧、朝目标直飞（不蓄势）、命中之后记刻印。
/// 解和捌的差别**不在这个文件里**——伤害谁高谁低、飞多快、多大、什么颜色，都是放它的人给的参数。
/// </summary>
public partial class SlashProjectile : DamageProjectile
{
    /// <summary>一刀刻几格刻印（默认 1）。</summary>
    private int _marks = 1;

    /// <summary>刻痕的朝向（跟飞行方向一致，画出来才像"斩过去"）。</summary>
    private float _angle;

    /// <summary>
    /// 放出来之前调（进树之前）：伤害、来源、方向、速度、留场、大小、颜色、刻几格。
    /// </summary>
    public void Launch(Ball caster, float damage, string sourceType, Vector2 direction,
        float speed, float life, float size, Color color, int marks = 1)
    {
        _marks = Mathf.Max(marks, 0);

        Setup(caster, caster == null ? 0 : caster.Group, caster?.Id, damage, sourceType,
            direction, speed, 0f, life, size, color);

        // 整道斩痕跟着飞行方向转，画的时候就能按"横着的一道"来画
        _angle = Direction.Angle();
        Rotation = _angle;
    }

    /// <summary>命中：刻印 +1（这就是"每斩一下就涨一个数值"）。</summary>
    protected override void OnHitBall(Ball ball)
    {
        var mark = SlashMark.Find(Caster);
        if (mark != null)
        {
            int now = mark.Add(_marks);
            GD.Print($"[{SourceType}] {Name} 命中 {ball.Name}，斩击刻印 +{_marks}（现在 {now} 格）");
        }
    }

    /// <summary>
    /// 一道细长的斩痕：长边是 `Size`、短边只有它的 1/5，中间亮、边上淡——
    /// 没有素材，全是画出来的（想换样子就覆盖这个方法）。
    /// </summary>
    protected override void DrawShape()
    {
        float longSide = Size * 0.5f;
        float shortSide = longSide * 0.2f;

        var outer = new Vector2[28];
        for (int i = 0; i < outer.Length; i++)
        {
            float t = Mathf.Tau * i / outer.Length;
            outer[i] = new Vector2(Mathf.Cos(t) * longSide, Mathf.Sin(t) * shortSide);
        }

        DrawColoredPolygon(outer, BodyColor);

        // 中间那道更亮的芯：同样形状、小一圈
        var inner = new Vector2[28];
        for (int i = 0; i < inner.Length; i++)
        {
            float t = Mathf.Tau * i / inner.Length;
            inner[i] = new Vector2(Mathf.Cos(t) * longSide * 0.75f, Mathf.Sin(t) * shortSide * 0.4f);
        }

        var core = BodyColor;
        core.A = Mathf.Min(1f, BodyColor.A + 0.15f);
        core = core.Lerp(new Color(1f, 1f, 1f), 0.5f);
        DrawColoredPolygon(inner, core);
    }
}
