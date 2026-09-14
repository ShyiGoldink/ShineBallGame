using Godot;

/// <summary>
/// 屎蛋：撞到敌人时把它切成**受控状态**（减速的入口），然后连着几秒
/// 每秒造成对方最大生命值 1/100 的伤害（单次最多 100 点）；效果走完自己消失。
///
/// 它是 `Egg` 的第一个子类，也是"蛋不走数据配置"的样板：想加会飘的蛋、会炸的蛋，
/// 就再写一个 `Egg` 的子类，然后在 `EggLibrary` 里补一行编号。
/// </summary>
public partial class ShitEgg : Egg
{
    public override string Type => "egg.shit";

    protected override string Texture => "shit";

    protected override float Size => 60f;

    /// <summary>效果持续几秒：这段时间里对面受控，每秒跳一次伤害。</summary>
    public float Duration = 5f;

    /// <summary>隔几秒跳一次伤害。</summary>
    public float TickInterval = 1f;

    /// <summary>每次跳多少：对方最大生命值 × 这个比例（1/100）。</summary>
    public float DamageRatio = 0.01f;

    /// <summary>单次伤害上限。</summary>
    public float DamageCap = 100f;

    private Ball _target;
    private float _effectLeft;
    private float _tickLeft;

    protected override void OnHit(Ball enemy)
    {
        // 撞上就立刻疼一下，之后每秒一次；重复撞到把时间重新计时，不会叠加
        _target = enemy;
        _effectLeft = Duration;
        _tickLeft = 0f;

        HideShell(); // 壳收掉：它就在这儿等着被撞，撞到就等于"用掉了"

        // 切成受控状态：对面挂着控制类组件就由那边接手移动（减速），没挂就是定身
        enemy.BeControlled(Duration, ControlRepeat.Reset);

        GD.Print($"[{Type}] {Name} 让 {enemy.Name} 受控 {Duration} 秒，并开始每秒跳伤");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_target == null)
        {
            return;
        }

        if (!IsInstanceValid(_target) || _target.State == BallState.Dead)
        {
            Destroy(); // 目标没了，这颗蛋也没用了
            return;
        }

        _effectLeft -= (float)delta;
        if (_effectLeft <= 0f)
        {
            GD.Print($"[{Type}] {Name} 效果结束，蛋消失");
            Destroy();
            return;
        }

        _tickLeft -= (float)delta;
        if (_tickLeft > 0f)
        {
            return;
        }

        _tickLeft = TickInterval;

        float damage = Mathf.Min(_target.MaxHp * DamageRatio, DamageCap);

        // 来源：蛋自己没有球身份，所以填"下蛋那颗球的 id"和"哪种蛋"
        //（真正扣血、打日志的是对方身上的 4001）
        _target.TakeDamage(new DamageEvent(_target, damage, Type, null, OwnerId));
    }
}
