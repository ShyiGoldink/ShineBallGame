using Godot;

/// <summary>
/// 护盾条：把护盾量做成面板上的一行数（"护盾 320 / 400"）。
///
/// 它**依赖护盾组件**——用的是 `BallComponent.Requirements` 那套：
/// 球上没有 `defense.shield` 时，装配器会报错并且**不挂这个组件**，
/// 因为它自己声明了"没有护盾就没东西可显示"（依赖是声明，不是顺序，Json 里谁先谁后都一样）。
///
/// 面板那一边完全不认识护盾：小球身上有一张读数表（`Ball.Readouts`），
/// 组件装配时往里登记"标签 + 现算函数"，面板只管照着画、定时现问一遍。
/// 所以想让别的组件也上面板，加一条登记就行，面板一个字都不用改。
/// </summary>
public partial class ShieldGauge : BallComponent
{
    /// <summary>依赖的那个组件的 `Type` 名。声明和查找都用它，免得两边写歪。</summary>
    private const string ShieldType = "defense.shield";

    public override int Id => 3003;

    public override string Type => "defense.shield_gauge";

    public override string DisplayName => "护盾";

    public override string Description => "把护盾量登记到面板上显示（依赖 defense.shield）";

    /// <summary>前置组件：没有护盾，这一行就没有东西可显示。</summary>
    public override string[] Requirements => new[] { ShieldType };

    /// <summary>
    /// 装配时接线：在球身上找到护盾组件，然后把"护盾量"登记到球的读数表里。
    /// 登记的是**现算函数**，不是当前值——面板每次刷新现问，所以显示永远不过期。
    /// </summary>
    public override void Bind(Ball ball, string configId)
    {
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，读数登记不上。");
            return;
        }

        var shield = FindShield(ball);
        if (shield == null)
        {
            // 正常走不到这里：装配器按 Requirements 拦过了。
            // 留着是为了"运行中才挂上来"这种情况，别静默地什么都不显示。
            GD.PushError($"[{Type}] 球上没有 {ShieldType}，这行读数不显示。");
            return;
        }

        ball.Readouts.Add(new BallReadout(DisplayName, () => $"{shield.Left:0} / {shield.Max:0}"));
        // 这里**不要**把当前值读出来打日志：同一个球上，护盾组件的 Bind 可能还没跑
        // （取决于 Json 里的书写顺序），这一刻的值还不是最终值。面板拉到的是真值。
        GD.Print($"[{Type}] {ball.Name} 的护盾量登记到面板");
    }

    /// <summary>按 `Type` 名在球身上找护盾组件——依赖声明的就是这个名字。</summary>
    private static Shield FindShield(Ball ball)
    {
        foreach (var child in ball.GetChildren())
        {
            if (child is BallComponent component && component.Type == ShieldType)
            {
                return component as Shield;
            }
        }

        return null;
    }
}
