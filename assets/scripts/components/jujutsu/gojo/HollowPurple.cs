using Godot;

/// <summary>
/// 虚式「茈」（`2006`）：**场上同时有苍和赫时自动开这一发**。
///
/// 什么时候放（三条都满足才动手）：
/// * 场上同时有**自己放**的一颗苍和一颗赫，而且两颗都已经**到位**（`Active`，还在飞的还不算）；
/// * 自己**不在受控状态**（受控期间放不出来）、也没死；
/// * 有活着的敌对球可以瞄。
///
/// **这一发不看 cd**：它自己就是一个独立组件，跟随机出招那套（`2005`）没关系，
/// 苍/赫 一凑齐就能放——这就是"不受 cd 影响"。
///
/// 放的时候：
/// 1. 两颗苍/赫 收掉（在它们**中点**的位置"融合"）；
/// 2. 从那儿朝**最近的敌对球**的方向放一颗巨大的茈球（`Purple`），命中就按受伤链算巨额伤害；
/// 3. 自己进一次攻击状态（招式名 `move`，时长和动画走招式表）。
///
/// **解耦的地方**：它不认识 `2005` 随机出招那套、也不认识牵引组件，只认两样东西——
/// `JujutsuOrb` 这个契约（是苍还是赫、什么阵营、到位没有）和 `Purple` 这个"会飞的伤害包"。
/// 想改触发条件只改这个文件，想改茈球长什么样 / 打多少只改 `Purple` 和它的参数。
/// </summary>
public partial class HollowPurple : BallComponent
{
    private const string PurpleScene = "res://assets/scene/projectiles/Purple.tscn";

    public override int Id => 2006;

    public override string Type => "attack.hollow_purple";

    public override string DisplayName => "虚式茈";

    public override string Description => "场上同时有自己的苍和赫、且不受控时，融合成茈球朝敌人发出，命中造成巨额伤害";

    /// <summary>前置组件：放这一发要花咒力。</summary>
    public override string[] Requirements => new[] { CursedEnergy.TypeName };

    /// <summary>放一次花多少咒力（名义值，实际要乘咒力池的消耗倍率）。</summary>
    public float Cost = 500f;

    /// <summary>茈球命中的伤害。</summary>
    public float Damage = 10000f;

    /// <summary>茈球放出来之后先原地蓄势多久再冲（秒）。就是"冲击力"那一下的停顿。</summary>
    public float Delay = 0.5f;

    /// <summary>茈球的飞行速度（像素/秒）。</summary>
    public float Speed = 2000f;

    /// <summary>飞多久还没撞到人就消失（秒）；蓄势那段时间不算在里面。</summary>
    public float Life = 3f;

    /// <summary>茈球多大（像素，按素材长边算）。"巨大"就配大一点。</summary>
    public float Size = 280f;

    /// <summary>招式表里没配时长时，攻击状态演多久。</summary>
    public float AttackTime = 0.8f;

    /// <summary>这一招在球的招式表（`attacks`）里叫什么。</summary>
    public string Move = "purple";

    private Ball _ball;
    private CursedEnergy _pool;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Cost = JsonTool.GetValue(parameters, "cost", Cost);
        Damage = JsonTool.GetValue(parameters, "damage", Damage);
        Delay = JsonTool.GetValue(parameters, "delay", Delay);
        Speed = JsonTool.GetValue(parameters, "speed", Speed);
        Life = JsonTool.GetValue(parameters, "life", Life);
        Size = JsonTool.GetValue(parameters, "size", Size);
        AttackTime = JsonTool.GetValue(parameters, "attack_time", AttackTime);
        Move = JsonTool.GetValue(parameters, "move", Move);
    }

    /// <summary>装配时接线：拿到球和咒力池。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，放不出茈。");
            return;
        }

        _pool = FindPool(ball);
        if (_pool == null)
        {
            GD.PushError($"[{Type}] 球上没有 {CursedEnergy.TypeName}，放不出茈。");
            return;
        }

        GD.Print($"[{Type}] {ball.Name} 的虚式茈就绪：场上凑齐苍和赫就放（伤害 {Damage:0.#}）");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_ball == null || _pool == null || _ball.State == BallState.Dead)
        {
            return;
        }

        if (_ball.State == BallState.Controlled)
        {
            return; // 受控：放不出来（条件写在这儿，不写进状态机）
        }

        // 自己的一颗苍 + 一颗赫，都得已经到位
        var blue = FindOwnOrb(OrbKind.Attract);
        var red = FindOwnOrb(OrbKind.Repel);
        if (blue == null || red == null)
        {
            return;
        }

        if (FindNearestEnemy() == null)
        {
            return; // 没人可打（比如对面已经没了）：苍/赫 留着
        }

        Fire(blue, red);
    }

    /// <summary>融合 + 放茈球 + 进攻击状态。</summary>
    private void Fire(JujutsuOrb blue, JujutsuOrb red)
    {
        // 先付账：付不起就不放，苍/赫 留着（咒力池自己会打一句"咒力不够"）
        if (!_pool.TrySpend(Cost))
        {
            return;
        }

        var parent = _ball.GetParent();
        if (parent == null)
        {
            GD.PushError($"[{Type}] 球不在场景里，茈球没地方放。");
            return;
        }

        // 融合的位置：两颗苍/赫 的中点
        var origin = (blue.GlobalPosition + red.GlobalPosition) * 0.5f;

        GD.Print($"[{Type}] {_ball.Name} 的苍与赫融合，蓄势后重新索敌放出虚式茈");

        blue.QueueFree();
        red.QueueFree();

        var scene = GD.Load<PackedScene>(PurpleScene);
        if (scene == null)
        {
            GD.PushError($"[{Type}] 找不到茈球的预制体：{PurpleScene}");
            return;
        }

        var purple = scene.Instantiate<Purple>();
        purple.Name = "茈球";
        // 和苍/赫 挂在同一个父节点下，所以位置要换成相对父节点的局部坐标
        purple.Position = parent is Node2D parent2D ? origin - parent2D.GlobalPosition : origin;
        purple.Launch(_ball, _ball.Id, Damage, Speed, Delay, Life, Size);
        parent.AddChild(purple);

        // 出招：进一次攻击状态（时长和动画走招式表）
        float duration = AttackTime;
        if (MoveTable.TryGet(_ball.Id, Move, out float tableTime, out _) && tableTime > 0f)
        {
            duration = tableTime;
        }

        _ball.BeginAttack(Move, duration, AttackRepeat.Replace);
    }

    /// <summary>自己放的那种苍/赫 里，已经到位的一颗（没有就是 null）。</summary>
    private JujutsuOrb FindOwnOrb(OrbKind kind)
    {
        foreach (var node in GetTree().GetNodesInGroup(JujutsuOrb.OrbsGroup))
        {
            if (node is JujutsuOrb orb && orb.Kind == kind && orb.Active && orb.OwnerGroup == _ball.Group)
            {
                return orb;
            }
        }

        return null;
    }

    /// <summary>最近的活着的敌对球。</summary>
    private Ball FindNearestEnemy()
    {
        Ball best = null;
        float bestDistance = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup(Ball.BallsGroup))
        {
            if (node is not Ball ball || ball.Group == _ball.Group || ball.State == BallState.Dead)
            {
                continue;
            }

            float distance = ball.GlobalPosition.DistanceSquaredTo(_ball.GlobalPosition);
            if (distance < bestDistance)
            {
                best = ball;
                bestDistance = distance;
            }
        }

        return best;
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
