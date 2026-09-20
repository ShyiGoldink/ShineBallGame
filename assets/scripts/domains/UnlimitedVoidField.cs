using Godot;
using System.Collections.Generic;

/// <summary>
/// 无量空处**长什么样**：近黑的深紫底 + 白偏紫的环，圈里漂着一层白的点
/// （"无限的信息灌进来"那个感觉）。别的什么都不管，账和规则都在 `Domain` 里。
///
/// 它只是 `DomainField` 的一个配色 + 装饰：以后别人的领域想要别的样子，
/// 就照这个再写一个子类（跟"一种蛋一个类"一个路子）。
/// 现在的图全是代码画的，不占素材、不用过导入——等美术给了图再换成贴图也不影响规则。
/// </summary>
public partial class UnlimitedVoidField : DomainField
{
    /// <summary>圈里撒多少个点。</summary>
    private const int SpeckCount = 90;

    /// <summary>圈里一个漂着的点。</summary>
    private struct Speck
    {
        public float Angle;    // 起始角度
        public float Distance; // 离圆心多远（0~1，画的时候乘当前半径）
        public float Speed;    // 转多快
        public float Size;     // 多大
        public float Phase;    // 闪烁的相位（点之间错开）
    }

    private readonly List<Speck> _specks = new();
    private float _time;

    public UnlimitedVoidField()
    {
        // 无量空处的配色：底是近黑的紫，环是白偏紫
        FillColor = new Color(0.05f, 0.03f, 0.09f, 0.72f);
        RingColor = new Color(0.92f, 0.9f, 1f, 0.95f);
    }

    public override void _Ready()
    {
        for (int i = 0; i < SpeckCount; i++)
        {
            _specks.Add(new Speck
            {
                Angle = GD.Randf() * Mathf.Tau,
                // 开平方是为了铺得均匀：不处理的话点会挤在圆心附近
                Distance = Mathf.Sqrt(GD.Randf()),
                Speed = 0.05f + GD.Randf() * 0.25f,
                Size = 0.7f + GD.Randf() * 1.8f,
                Phase = GD.Randf() * Mathf.Tau,
            });
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        base._Process(delta);
    }

    /// <summary>圈里的点：内外两层反向慢慢转、各自错开地闪。</summary>
    protected override void DrawInside(float radius)
    {
        foreach (var speck in _specks)
        {
            // 外面一层倒着转，里面一层顺着转 —— 看着像有东西在里面流动
            float direction = speck.Distance > 0.55f ? -1f : 1f;
            float angle = speck.Angle + _time * speck.Speed * direction;

            var position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * speck.Distance);
            float twinkle = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(_time * 1.7f + speck.Phase));

            DrawCircle(position, speck.Size, new Color(1f, 1f, 1f, 0.75f * twinkle));
        }
    }
}
