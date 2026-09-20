using Godot;

/// <summary>
/// 无量空处（`8001`）：五条悟的领域展开。**它自己只填了几个默认值**——
/// 规则全在基类 `Domain` 里（怎么开、外壳怎么掉、范围怎么互相压、收场怎么熔断），
/// 这里只定"这个领域的个性"：长什么样（`UnlimitedVoidField`：近黑的紫底 + 白环 + 漂的点）、
/// 多大、多强、开一次花多少咒力。
///
/// 效果**不在这个文件里**：被无量空处罩住会怎样由给对面的组件负责——
/// 五条悟的 `enemycomponents` 里配的是 `8002 domain.control`（`duration` 100000 =
/// 中了这一局就废了）和 `6002 control.mute_attack`（受控期间不能靠碰撞还手）。
/// 所以想改"控多久""还能不能还手"，改的是 Json，不是这儿。
///
/// 默认值（都能被 `balldata.json` 里的同名键覆盖）：
/// 半径 800、优先级 20、外壳 = 最大血量的一半（五条 12000 血 → 6000）、持续 99 秒、
/// 名义消耗 600 咒力（他配了六眼的消耗倍率 0.01，实际只花 6）、收场熔断 20 秒。
/// </summary>
public partial class UnlimitedVoid : Domain
{
    public override int Id => 8001;

    public override string Type => "domain.unlimited_void";

    public override string DisplayName => "无量空处";

    public override string Description => "五条悟的领域：罩住圈里的敌人（效果由送对面的 8002 定）；带外壳，挨够半血就碎";

    /// <summary>展开要花咒力，所以依赖咒力池。</summary>
    public override string[] Requirements => new[] { CursedEnergy.TypeName };

    public UnlimitedVoid()
    {
        Radius = 800f;
        Priority = 20;
        Duration = 99f;
        Cost = 600f;
        Windup = 0.6f;
        Move = "domain";
        BurnoutTime = 20f;
    }

    /// <summary>场的样子：无量空处那一套配色和圈里漂的点。</summary>
    protected override DomainField CreateField() => new UnlimitedVoidField();
}
