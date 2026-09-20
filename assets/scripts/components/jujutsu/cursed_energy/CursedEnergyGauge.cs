using Godot;

/// <summary>
/// 咒力条（`7002`）：把咒力池显示成**球身上的一根条 + 面板上的一行数**。
///
/// 它依赖咒力基础组件（`jujutsu.cursed_energy`）：球上没有那个池子时，装配器会报错
/// 并且不挂这个组件——没有池子就没有东西可显示。
///
/// 它也是"**加一根条不用动预制体**"的样板：条是它自己在装配时造出来、挂到球身上的
/// 条区（`BallLayout`）下面的。尺寸由条自己带，排在哪儿归条区，预制体里不用为它留节点。
/// 以后要显示别的资源（气势、怒气……），照这个组件再写一个就行。
/// </summary>
public partial class CursedEnergyGauge : BallComponent
{
    /// <summary>自己造的那根条在球上的节点名（调试、以后想从球上找这条时用）。</summary>
    public const string BarName = "CursedEnergyBar";

    public override int Id => 7002;

    public override string Type => "jujutsu.cursed_energy_gauge";

    public override string DisplayName => "咒力";

    public override string Description => "把咒力池显示成球身上的一根条和面板上一行数（依赖 jujutsu.cursed_energy）";

    /// <summary>前置组件：没有咒力池，这一行数和这根条都没东西可显示。</summary>
    public override string[] Requirements => new[] { CursedEnergy.TypeName };

    private CursedEnergy _pool;
    private CursedEnergyBar _bar;

    public override void Bind(Ball ball, string configId)
    {
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，咒力条挂不上。");
            return;
        }

        var pool = FindPool(ball);
        if (pool == null)
        {
            // 正常走不到这里：装配器按 Requirements 拦过了。
            // 留着是为了"运行中才挂上来"这种情况，别静默地什么都不显示。
            GD.PushError($"[{Type}] 球上没有 {CursedEnergy.TypeName}，咒力条显示不出来。");
            return;
        }

        var layout = BallLook.Layout(ball);
        if (layout == null)
        {
            GD.PushError($"[{Type}] 预制体里没有条区（{BallLook.LayoutPath}），咒力条挂不上。");
            return;
        }

        _pool = pool;

        // 条自己造、自己带尺寸；挂在条区最后 = 排在最下面
        _bar = new CursedEnergyBar { Name = BarName };
        layout.AddChild(_bar);

        // 面板那一行数：登记的是**现算函数**，不是当前值——
        // 装配时这个池子的 Bind 可能还没跑，那一刻读到的还不是最终值。
        // （球身上那根条不用登记，它在 _Process 里每帧现推。）
        ball.Readouts.Add(new BallReadout(DisplayName, () => $"{pool.Current:0} / {pool.Max:0}"));

        GD.Print($"[{Type}] {ball.Name} 的咒力条挂到条区，咒力那一行数也登记到面板了");
    }

    /// <summary>每帧把池子里的量推给条：面板 10Hz 轮询够用，球身上的条得跟手一点。</summary>
    public override void _Process(double delta)
    {
        if (_bar != null && _pool != null)
        {
            _bar.SetEnergy(_pool.Current, _pool.Max);
        }
    }

    /// <summary>按 `Type` 名在球身上找咒力池——依赖声明的就是这个名字。</summary>
    private static CursedEnergy FindPool(Ball ball)
    {
        foreach (var child in ball.GetChildren())
        {
            if (child is BallComponent component && component.Type == CursedEnergy.TypeName)
            {
                return component as CursedEnergy;
            }
        }

        return null;
    }
}
