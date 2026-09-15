using Godot;

/// <summary>
/// 平A（`2004`）：**撞击型的基础攻击**。伤害按咒力状态分两档，还有概率打出黑闪。
///
/// 参数：
/// * `damage` —— 平时的基础伤害（五条悟 500）；
/// * `damage_burnout` —— **术式熔断**时的伤害（五条悟 100）；熔断问咒力池（`BurnedOut`）；
/// * `black_flash_chance` —— 这一下出黑闪的概率（0~1，五条悟 0.04，两档伤害都适用）；
/// * `black_flash_multiplier` —— 黑闪的**倍率**：打出来这一下就乘它（五条悟 2.5，500 变 1250）；
/// * `sound` / `pitch` —— 跟 `2001 attack.collision` 一样（黑闪想单独一个音效以后再说）。
///
/// 黑闪不是"另一种攻击"，而是这一下的**结果**：先按状态取基础伤害、乘上黑闪带来的伤害加成，
/// 抽中了再乘黑闪倍率，最后让咒力池记一层黑闪（`CursedEnergy.EnterBlackFlash`）。
/// **黑闪之后的那些加成不在这个文件里**：池子按层数自己算（反转术式效率、咒力恢复、
/// 消耗倍率），伤害这种池子管不着的用 `_pool.Multiplier("damage")` 来拿一格
/// （配置里没写 `damage` 就是 1，等于没有加成）。
///
/// 其它规矩和 `2001` 一样：不看攻击状态（任何时候都能打）、撞到自己不算、同阵营不算、
/// 自己死了不打人、音效只在"这一下真打出去了"的时候响。
/// </summary>
public partial class BasicAttack : BallComponent
{
    public override int Id => 2004;

    public override string Type => "attack.basic";

    public override string DisplayName => "平A";

    public override string Description => "撞击型的基础攻击：伤害按熔断分两档，有概率出黑闪（伤害取指数），黑闪后永久提高反转术式效率";

    /// <summary>前置组件：熔断状态和黑闪加成都在咒力池里。</summary>
    public override string[] Requirements => new[] { CursedEnergy.TypeName };

    /// <summary>平时的基础伤害。</summary>
    public float Damage = 100f;

    /// <summary>术式熔断时的基础伤害。</summary>
    public float BurnoutDamage = 100f;

    /// <summary>出黑闪的概率（0~1）。0 = 永远不出（调试和做平衡的时候很方便）。</summary>
    public float BlackFlashChance;

    /// <summary>黑闪的倍率：打出来这一下就乘它。2.5 = 伤害变成 2.5 倍（500 变 1250）。</summary>
    public float BlackFlashMultiplier = 2.5f;

    /// <summary>打到敌人时播的音效，写文件名或路径；留空就不播。</summary>
    public string Sound = string.Empty;

    /// <summary>音效的播放音高：1 = 原样，小于 1 更低沉。</summary>
    public float Pitch = 1f;

    /// <summary>阵营，装配时从球身上填（跟 `2001` 一样，用来判断敌我）。</summary>
    public int Group;

    private Ball _ball;
    private CursedEnergy _pool;
    private bool _alive = true;

    /// <summary>哪颗球的 Json 配出了我（从 enemycomponents 送人时是送出去的那颗球）。</summary>
    private string _configId;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Damage = JsonTool.GetValue(parameters, "damage", Damage);
        BurnoutDamage = JsonTool.GetValue(parameters, "damage_burnout", BurnoutDamage);
        BlackFlashChance = JsonTool.GetValue(parameters, "black_flash_chance", BlackFlashChance);
        BlackFlashMultiplier = JsonTool.GetValue(parameters, "black_flash_multiplier", BlackFlashMultiplier);
        Sound = JsonTool.GetValue(parameters, "sound", Sound);
        Pitch = JsonTool.GetValue(parameters, "pitch", Pitch);
    }

    /// <summary>装配时接线：阵营跟着挂我的那颗球走；接上检测圈；订状态变化（死了就不再打人）。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        _configId = configId;

        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，平A打不出去。");
            return;
        }

        Group = ball.Group;

        _pool = FindPool(ball);
        if (_pool == null)
        {
            GD.PushError($"[{Type}] 球上没有 {CursedEnergy.TypeName}，熔断分档和黑闪都用不了。");
            return;
        }

        var hitArea = ball.GetNodeOrNull<Area2D>("HitArea");
        if (hitArea == null)
        {
            GD.PushError("[装配] 预制体里找不到 HitArea，这次平A接不上。");
        }
        else
        {
            hitArea.BodyEntered += OnBodyEntered;
        }

        _ball.Events.Register(EventName.state_changed, new EventResponseFunction { priority = 0, action = OnStateChanged });
        _alive = _ball.State != BallState.Dead;
    }

    private bool OnStateChanged(object arg)
    {
        // 只有死亡算"不能打人"，登场/移动/受控/攻击都照常
        _alive = arg is not BallState state || state != BallState.Dead;
        return true;
    }

    /// <summary>撞击：给对方造成这一下的伤害（可能带黑闪）。每次接触只触发一次。</summary>
    public void OnBodyEntered(Node2D body)
    {
        if (!_alive || _pool == null)
        {
            return; // 已经死了（或者没接上咒力池），不能打人
        }

        // 撞到自己不算：球自己的检测区域有可能扫到自己的碰撞盒
        if (body is not Ball enemy || enemy.GetInstanceId() == GetParent().GetInstanceId())
        {
            return;
        }

        if (enemy.Group == Group)
        {
            return; // 同阵营不算敌人
        }

        // 一、先按状态取基础伤害（熔断期间是另一档），再乘上黑闪带来的伤害加成
        //（配置里没写 damage 这项加成时，Multiplier 就是 1，等于没加成）
        float baseDamage = _pool.BurnedOut ? BurnoutDamage : Damage;
        float damage = baseDamage * _pool.Multiplier(CursedEnergy.DamageKey);

        // 二、抽黑闪：抽中了这一下再乘黑闪倍率
        bool blackFlash = BlackFlashChance > 0f && GD.Randf() < BlackFlashChance;
        if (blackFlash)
        {
            damage *= BlackFlashMultiplier;
        }

        enemy.TakeDamage(new DamageEvent(enemy, damage, Type, _ball));

        // 音效跟着"这一下真打出去了"走，前面那些不算数的碰撞都不会响
        SoundTool.PlayOnce(this, Sound, _configId ?? _ball?.Id, Pitch);

        if (!blackFlash)
        {
            return;
        }

        GD.Print($"[{Type}] {_ball.Name} 打出黑闪：{baseDamage:0.#} × {BlackFlashMultiplier:0.##} = {damage:0.#}"
            + $"（{(_pool.BurnedOut ? "熔断期" : "平常")}）");

        // 三、黑闪结算：池子记一层黑闪，之后所有加成（含反转术式效率）都跟着层数自动走
        _pool.EnterBlackFlash();
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
