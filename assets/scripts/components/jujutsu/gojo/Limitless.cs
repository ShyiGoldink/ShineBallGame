using Godot;

/// <summary>
/// 无下限（`3004`）：把护盾变成"打不穿"的那层东西——**只要术式没熔断、咒力还够，
/// 就每秒把护盾灌回一大截**，代价是每秒烧掉一笔咒力。
///
/// 它**不自己存护盾，而是站在护盾组件上**：护盾还是原来那一套（挨打先掉盾、
/// 盾没挡住的才进血），这里只负责"往盾里灌"。所以球上必须同时有护盾和咒力池，
/// `Requirements` 里两个都写了：
///
/// * `defense.shield` —— 灌的是它的盾；
/// * `jujutsu.cursed_energy` —— 烧的是它的咒力，熔断状态也问它。
///
/// **什么时候灌、什么时候不灌**（三条停手规则，按顺序）：
/// 1. 术式熔断（`BurnedOut`）时完全不灌——这是"术式"的总开关，状态只存在咒力池里；
/// 2. 盾已经满了不灌——**只有真的在灌的时候才烧咒力**，所以盾满着的时候咒力一点不掉
///    （这就是"只有护盾恢复时才会每秒消耗"）；
/// 3. 这一帧该付的咒力付不起时不灌（先付账后灌盾，不会白嫖）。
///
/// 六眼那种咒力消耗倍率（咒力池的 `cost_rate`）配得极低（0.01），于是同样的
/// 100/秒实际只花 1/秒——"对六眼来说相当于无损耗"就是这么来的，不用在这里特判。
/// </summary>
public partial class Limitless : BallComponent
{
    /// <summary>依赖的组件名。声明和查找都用它，免得两边写歪。</summary>
    private const string ShieldType = "defense.shield";

    public override int Id => 3004;

    public override string Type => "defense.limitless";

    public override string DisplayName => "无下限";

    public override string Description => "术式没熔断、咒力还够时，每秒把护盾灌回一大截，代价是每秒烧咒力";

    /// <summary>前置组件：护盾（灌的就是它）和咒力池（烧的是它、熔断也问它）。</summary>
    public override string[] Requirements => new[] { ShieldType, CursedEnergy.TypeName };

    /// <summary>每秒往护盾里灌多少。"巨量"就是配得比挨打的伤害快得多——打得没回得快，就打不穿。</summary>
    public float Regen = 800f;

    /// <summary>每秒烧多少咒力（名义值，实际花多少还要乘咒力池的消耗倍率）。</summary>
    public float Cost = 100f;

    private Ball _ball;
    private Shield _shield;
    private CursedEnergy _pool;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Regen = JsonTool.GetValue(parameters, "regen", Regen);
        Cost = JsonTool.GetValue(parameters, "cost", Cost);
    }

    /// <summary>
    /// 装配时接线：把护盾和咒力池找出来。
    /// 这一步在所有组件都挂到球上之后才跑（见装配器的说明），所以按 `Type` 找兄弟组件
    /// 跟 Json 里的书写顺序无关。
    /// </summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，无下限不生效。");
            return;
        }

        _shield = Find<Shield>(ball, ShieldType);
        _pool = Find<CursedEnergy>(ball, CursedEnergy.TypeName);

        if (_shield == null || _pool == null)
        {
            // 正常走不到这里：装配器按 Requirements 拦过了。
            // 留着是为了"运行中才挂上来"这种情况，别静默地什么都不做。
            GD.PushError($"[{Type}] 球上没有 {ShieldType} 或 {CursedEnergy.TypeName}，无下限不生效。");
            return;
        }

        GD.Print($"[{Type}] {ball.Name} 的无下限挂好了：每秒灌 {Regen:0.#} 护盾，烧 {Cost:0.#} 咒力");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_shield == null || _pool == null || Regen <= 0f || delta <= 0.0)
        {
            return;
        }

        if (_pool.BurnedOut)
        {
            return; // 1. 熔断：术式本身停了
        }

        float missing = _shield.Max - _shield.Left;
        if (missing <= 0f)
        {
            return; // 2. 盾是满的：不灌，也就不烧咒力
        }

        // 这一帧该灌多少：按每秒量算，但别灌过头（盾快满的那一帧只灌得起这么多）
        float amount = Mathf.Min(Regen * (float)delta, missing);

        // 花销按"真的灌了多少"算：灌了一帧的量就付一帧的钱，盾快满的那一帧只灌了一点、
        // 就只付那一点（换算成秒数 = 灌的量 ÷ 每秒量），不会为没灌上的部分白付钱
        float seconds = amount / Regen;

        // 3. 先付账：咒力不够这一帧的花销就不灌
        if (!_pool.TrySpend(Cost * seconds))
        {
            return;
        }

        _shield.Restore(amount);
    }

    /// <summary>按 `Type` 名在球身上找一个组件——依赖声明的就是这个名字。</summary>
    private static T Find<T>(Ball ball, string type) where T : BallComponent
    {
        foreach (var child in ball.GetChildren())
        {
            if (child is T component && component.Type == type)
            {
                return component;
            }
        }

        return null;
    }
}
