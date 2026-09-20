using Godot;

/// <summary>
/// 伏魔御厨子（`8003`）：宿儺的领域展开，**开放型**——没有外壳，所以打不碎、
/// 也不参与优先级的范围比较，只会按时间到期；但**照样替持有者抵消对面的必中**
/// （开放型不吃伤害，"抵消"这件事由 `Domain.Absorb` 处理）。
///
/// 和 `UnlimitedVoid` 一样，这个文件只填"这个领域的个性"：长什么样（`MalevolentShrineField`）、
/// 多大、多强、开一次花多少咒力。**谁在圈里会怎样不在这里**——那是给对面的 `8004`
/// （每 1 秒一次必中斩）的事。
/// </summary>
public partial class MalevolentShrine : Domain
{
    public override int Id => 8003;

    public override string Type => "domain.malevolent_shrine";

    public override string DisplayName => "伏魔御厨子";

    public override string Description => "宿儺的领域：开放型（没有外壳、打不碎），圈里的人每 1 秒挨一次必中斩";

    /// <summary>展开要花咒力，所以依赖咒力池。</summary>
    public override string[] Requirements => new[] { CursedEnergy.TypeName };

    public MalevolentShrine()
    {
        Open = true;        // 没有壳：打不碎，只按时间到期
        Radius = 1200f;     // 比无量空处大一圈（原作里它的范围也更夸张：能罩住整座城）
        Priority = 20;      // 和无量空处同级：谁也压不动谁，就是拼谁的壳先碎
        Duration = 99f;
        Cost = 6000f;
        Windup = 0.8f;
        Move = "domain";
        BurnoutTime = 20f;
    }

    /// <summary>场的样子：暗红黑的圈 + 骨白的斩痕，环画得虚（开放型）。</summary>
    protected override DomainField CreateField() => new MalevolentShrineField();
}
