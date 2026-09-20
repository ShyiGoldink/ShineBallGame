using Godot;
using System.Collections.Generic;

/// <summary>
/// 伏魔御厨子**长什么样**：开放型领域——**没有壳，所以环画得虚**（一眼看出"这个打不碎"），
/// 圈里是暗红发黑的底，浮着几道骨白色的斩痕。
///
/// 和 `UnlimitedVoidField` 一个定位：只管画，规则（谁能进来、壳掉不掉、什么时候碎）
/// 全在 `Domain` 里。想换配色就改这个文件，不用碰任何逻辑。
/// </summary>
public partial class MalevolentShrineField : DomainField
{
    /// <summary>圈里画几道斩痕。</summary>
    private const int StreakCount = 14;

    /// <summary>一道斩痕的位置和长短。</summary>
    private struct Streak
    {
        public Vector2 Center;  // 位置（0~1 的圈里，画的时候乘半径）
        public float Angle;     // 朝向
        public float Length;    // 多长（半径的比例）
        public float Phase;     // 闪烁的相位
    }

    private readonly List<Streak> _streaks = new();
    private float _time;

    public MalevolentShrineField()
    {
        // 伏魔御厨子的配色：底是暗红黑，斩痕是骨白
        FillColor = new Color(0.10f, 0.04f, 0.05f, 0.66f);
        RingColor = new Color(0.95f, 0.92f, 0.86f, 0.9f);
    }

    public override void _Ready()
    {
        for (int i = 0; i < StreakCount; i++)
        {
            // 开平方是为了铺得均匀（不处理的话会挤在圆心）
            float distance = Mathf.Sqrt(GD.Randf()) * 0.92f;
            float angle = GD.Randf() * Mathf.Tau;

            _streaks.Add(new Streak
            {
                Center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance,
                Angle = GD.Randf() * Mathf.Pi,          // 斩痕的朝向是随意的（不是放射状）
                Length = 0.10f + GD.Randf() * 0.22f,
                Phase = GD.Randf() * Mathf.Tau,
            });
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        base._Process(delta);
    }

    /// <summary>圈里的斩痕：一会儿亮一会儿淡，位置不动（像刚被切过、还没散）。</summary>
    protected override void DrawInside(float radius)
    {
        foreach (var streak in _streaks)
        {
            var direction = new Vector2(Mathf.Cos(streak.Angle), Mathf.Sin(streak.Angle));
            var center = streak.Center * radius;
            var half = direction * (streak.Length * radius * 0.5f);

            float blink = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_time * 1.3f + streak.Phase));
            var color = new Color(1f, 0.98f, 0.94f, 0.55f * blink);

            DrawLine(center - half, center + half, color, 2f, true);
        }
    }
}
