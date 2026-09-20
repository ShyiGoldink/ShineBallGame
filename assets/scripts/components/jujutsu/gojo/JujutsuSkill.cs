using Godot;

/// <summary>
/// 术式出招（`2005`）：**每 `cd` 秒按权重随机挑一招打出去**（五条悟的攻击手段）。
///
/// 现在只有一招：**苍 / 赫**（`blue_red`）——
/// * 场上有苍 → 朝着那颗苍放**赫**；
/// * 场上有赫 → 朝着那颗赫放**苍**；
/// * 场上什么都没有 → 苍、赫随机，飞的方向也随机。
///
/// 判"场上有苍/赫"时**只认自己放的那几颗**（两个五条对打时不会去瞄对面的苍赫），
/// 自己刚放的那颗当然也算，所以"先放苍、下一发朝它放赫"这个连招是天然成立的。
///
/// 挑招是按**权重**抽的（`skills` 表：招式名 → 权重，不写就是 1）。以后加招式就往表里加一行、
/// 代码里加一个 `case`；现在表里只有 `blue_red`。
///
/// **不认识的招式名照样演**（见 `CastByTable`）：付账、进攻击状态、时长和动画都走招式表，
/// 效果交给订了 `attack_started` 的组件去做——领域展开就是这么接上的
/// （招式表里挑中 `domain`，`8001` 认领之后展开）。所以加一招不一定非得改这个文件：
/// 只在 Json 的 `skills` 里加名字、再写一个认领它的组件也算数。
///
/// 规矩：
/// * **术式熔断期间不出招**（问咒力池的 `BurnedOut`），计时停在 0，等术式能用了立刻补上；
/// * 只在**移动状态**出招——登场、受控、攻击中到点了就等着，一回到移动立刻补上（和 `2002` 一个路子）；
/// * 出招时花**一次**咒力（`cost`，名义值，实际要乘咒力池的消耗倍率）；
/// * 出招会进一次攻击状态，时长和动画都走招式表（`attacks` 里那一条）。
/// </summary>
public partial class JujutsuSkill : BallComponent
{
    private const string BlueRedSkill = "blue_red";

    private const string OrbScene = "res://assets/scene/orbs/Orb.tscn";

    public override int Id => 2005;

    public override string Type => "attack.jujutsu_skill";

    public override string DisplayName => "术式出招";

    public override string Description => "每 cd 秒按权重随机出一招；现在只有苍/赫（朝场上的苍/赫反向释放，都没有就随机）";

    /// <summary>前置组件：咒力池（熔断状态和出招要花的咒力都在它那儿）。</summary>
    public override string[] Requirements => new[] { CursedEnergy.TypeName };

    /// <summary>两次出招的间隔（秒）。</summary>
    public float Cd = 10f;

    /// <summary>出招一次花多少咒力（名义值，实际要乘咒力池的消耗倍率）。</summary>
    public float Cost = 200f;

    /// <summary>招式表里没配时长时，攻击状态演多久。</summary>
    public float AttackTime = 0.5f;

    /// <summary>这一招在球的招式表（`attacks`）里叫什么；配了就用表里的时长和动画。</summary>
    public string Move = BlueRedSkill;

    /// <summary>苍/赫 飞多远之后停下。</summary>
    public float Distance = 400f;

    /// <summary>苍/赫 的飞行速度（像素/秒）。</summary>
    public float Speed = 600f;

    /// <summary>苍/赫 在场上留多久（秒）。</summary>
    public float Life = 30f;

    /// <summary>招式权重表（招式名 → 权重）。没配就是"只有苍/赫，权重 1"。</summary>
    public Godot.Collections.Dictionary Skills = new() { { BlueRedSkill, 1f } };

    private Ball _ball;
    private CursedEnergy _pool;
    private float _cdLeft;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Cd = JsonTool.GetValue(parameters, "cd", Cd);
        Cost = JsonTool.GetValue(parameters, "cost", Cost);
        AttackTime = JsonTool.GetValue(parameters, "attack_time", AttackTime);
        Move = JsonTool.GetValue(parameters, "move", Move);
        Distance = JsonTool.GetValue(parameters, "distance", Distance);
        Speed = JsonTool.GetValue(parameters, "speed", Speed);
        Life = JsonTool.GetValue(parameters, "life", Life);

        var skills = JsonTool.GetValue(parameters, "skills", new Godot.Collections.Dictionary());
        if (skills.Count > 0)
        {
            Skills = skills;
        }
    }

    /// <summary>装配时接线：拿到球和咒力池，CD 设成满的。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，出不了招。");
            return;
        }

        _pool = FindPool(ball);
        if (_pool == null)
        {
            GD.PushError($"[{Type}] 球上没有 {CursedEnergy.TypeName}，出不了招。");
            return;
        }

        _cdLeft = Cd;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_ball == null || _pool == null || _ball.State == BallState.Dead)
        {
            return;
        }

        _cdLeft -= (float)delta;
        if (_cdLeft > 0f)
        {
            return;
        }

        // 到点了但打不了（熔断 / 不在移动状态）：计时停在 0，等能打的那一刻立刻补上
        if (_pool.BurnedOut || _ball.State != BallState.Move)
        {
            _cdLeft = 0f;
            return;
        }

        _cdLeft = Cd;
        Cast(PickSkill());
    }

    /// <summary>按权重抽一招。表写坏了（权重全是 0 或没有表）就退回唯一的那一招。</summary>
    private string PickSkill()
    {
        float total = 0f;
        foreach (var key in Skills.Keys)
        {
            total += Mathf.Max(Skills[key].AsSingle(), 0f);
        }

        if (total <= 0f)
        {
            return BlueRedSkill;
        }

        float roll = GD.Randf() * total;
        foreach (var key in Skills.Keys)
        {
            roll -= Mathf.Max(Skills[key].AsSingle(), 0f);
            if (roll <= 0f)
            {
                return key.ToString();
            }
        }

        return BlueRedSkill;
    }

    private void Cast(string skill)
    {
        switch (skill)
        {
            case BlueRedSkill:
                CastBlueRed();
                break;
            default:
                CastByTable(skill);
                break;
        }
    }

    /// <summary>
    /// 招式表里有、这个文件里没写死效果的那一招：**照样演出来**——付一次账、进攻击状态，
    /// 时长和动画都走招式表（表里没这一项就用 `attack_time`）。
    /// 效果由**认领这个招式名的组件**去做：它订 `attack_started`，看到自己的名字就干活
    /// （领域展开 `8001` 就是这么接的）。所以这一招没效果不是 bug，是还没人认领。
    /// </summary>
    private void CastByTable(string skill)
    {
        if (!_pool.TrySpend(Cost))
        {
            GD.Print($"[{Type}] {_ball.Name} 咒力不够，这一招没放出来");
            return;
        }

        float duration = AttackTime;
        if (MoveTable.TryGet(_ball.Id, skill, out float tableTime, out _) && tableTime > 0f)
        {
            duration = tableTime;
        }

        _ball.BeginAttack(skill, duration, AttackRepeat.Replace);

        GD.Print($"[{Type}] {_ball.Name} 演了一招「{skill}」（效果由认领这个招式名的组件负责）");
    }

    /// <summary>苍 / 赫：朝场上已有的苍/赫反向放一颗，什么都没有就随机。</summary>
    private void CastBlueRed()
    {
        // 先付账：出招花一次咒力，付不起就不出（也不进攻击状态）
        if (!_pool.TrySpend(Cost))
        {
            GD.Print($"[{Type}] {_ball.Name} 咒力不够，这一招没放出来");
            return;
        }

        var (kind, direction) = PickOrbTarget();

        // 出招：进一次攻击状态（时长和动画都走招式表）
        float duration = AttackTime;
        if (MoveTable.TryGet(_ball.Id, Move, out float tableTime, out _) && tableTime > 0f)
        {
            duration = tableTime;
        }

        _ball.BeginAttack(Move, duration, AttackRepeat.Replace);
        SpawnOrb(kind, direction);

        GD.Print($"[{Type}] {_ball.Name} 放了{(kind == OrbKind.Attract ? "苍" : "赫")}"
            + $"，朝 ({direction.X:0.##}, {direction.Y:0.##}) 飞 {Distance:0.#} 像素");
    }

    /// <summary>
    /// 这一发该放苍还是赫、往哪飞：
    /// 有苍就朝苍放赫，有赫就朝赫放苍，都没有就苍/赫 和方向都随机。
    /// </summary>
    private (OrbKind kind, Vector2 direction) PickOrbTarget()
    {
        var attract = FindNearestOrb(OrbKind.Attract);
        if (attract != null)
        {
            return (OrbKind.Repel, AimAt(attract));
        }

        var repel = FindNearestOrb(OrbKind.Repel);
        if (repel != null)
        {
            return (OrbKind.Attract, AimAt(repel));
        }

        var kind = GD.Randf() < 0.5f ? OrbKind.Attract : OrbKind.Repel;
        return (kind, RandomDirection());
    }

    /// <summary>
    /// 自己放的、最近的某种苍/赫（**只认自己放的**：两个五条对打时各瞄各的，不会互相抢）。
    /// </summary>
    private JujutsuOrb FindNearestOrb(OrbKind kind)
    {
        JujutsuOrb best = null;
        float bestDistance = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup(JujutsuOrb.OrbsGroup))
        {
            if (node is not JujutsuOrb orb || orb.Kind != kind || orb.OwnerGroup != _ball.Group)
            {
                continue; // 不是自己放的：不算（那是对面在布场）
            }

            float distance = orb.GlobalPosition.DistanceSquaredTo(_ball.GlobalPosition);
            if (distance < bestDistance)
            {
                best = orb;
                bestDistance = distance;
            }
        }

        return best;
    }

    private Vector2 AimAt(JujutsuOrb orb)
    {
        var to = orb.GlobalPosition - _ball.GlobalPosition;
        return to.LengthSquared() > 0.01f ? to.Normalized() : RandomDirection();
    }

    /// <summary>随机方向：斜着飞（两个分量都不太小），免得正好压在水平/竖直线上。</summary>
    private static Vector2 RandomDirection()
    {
        float angle = Mathf.DegToRad(20f + GD.Randf() * 50f);
        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        int quadrant = (int)(GD.Randi() % 4);
        if (quadrant is 1 or 2)
        {
            direction.X = -direction.X;
        }

        if (quadrant is 2 or 3)
        {
            direction.Y = -direction.Y;
        }

        return direction;
    }

    private void SpawnOrb(OrbKind kind, Vector2 direction)
    {
        var parent = _ball.GetParent();
        if (parent == null)
        {
            GD.PushError($"[{Type}] 球不在场景里，苍/赫 没地方放。");
            return;
        }

        var scene = GD.Load<PackedScene>(OrbScene);
        if (scene == null)
        {
            GD.PushError($"[{Type}] 找不到苍/赫 的预制体：{OrbScene}");
            return;
        }

        var orb = scene.Instantiate<Orb>();
        orb.Name = kind == OrbKind.Attract ? "苍球" : "赫球";
        orb.Position = _ball.Position;
        orb.SetOwner(_ball.Group); // 阵营：苍/赫 只影响别的阵营的球
        orb.Setup(kind, direction, Distance, Speed, Life);
        parent.AddChild(orb);
    }

    /// <summary>按 `Type` 名在球身上找咒力池——依赖声明的就是这个名字。</summary>
    private static CursedEnergy FindPool(Ball ball)
    {
        foreach (var child in ball.GetChildren())
        {
            if (child is CursedEnergy pool)
            {
                return pool;
            }
        }

        return null;
    }
}
