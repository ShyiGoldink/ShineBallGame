using Godot;

/// <summary>
/// 护盾：挨打时先用护盾顶，顶不住的才继续往后传（最后由 `4001` 从血量里扣）。
///
/// 它占的是受伤链上 `DamagePriority.Shield`（700）那一格——排在无敌（1000）后面，
/// 中毒染色（600）和普通扣血（500）前面，所以被护盾吃掉的伤害，后面几环就看不见了。
///
/// **它改的是 `Amount`，不是自己扣血**：扣血只有 `4001` 干，"剩多少伤害"是这条链上
/// 唯一的公共账本。这里读 `Amount` 而不是 `Original`——护盾应该吃"前面几环减完之后"
/// 的伤害（比如以后有减伤组件，护盾不该按原始值扣）。
///
/// 参数：`amount` 护盾上限（默认 300），`regen` 每秒自动回多少（默认 0 = 不回）。
/// 它**不阻塞**（返回 true）：护盾把伤害吃成 0 也让 `4001` 照常走一遍，
/// 这样"这一下扣了多少血"只有一处日志（那一行会显示 -0）。
/// </summary>
public partial class Shield : BallComponent
{
    public override int Id => 3002;

    public override string Type => "defense.shield";

    public override string Description => "先掉护盾，护盾吃完的伤害才传到血量上；可选每秒自动回盾";

    /// <summary>护盾上限。开局就是满的。</summary>
    public float Amount = 300f;

    /// <summary>每秒自动回多少护盾，0 = 不回。</summary>
    public float Regen;

    private Ball _ball;

    /// <summary>当前还剩多少盾。</summary>
    private float _shieldLeft;

    /// <summary>现在还剩多少盾。要显示护盾量的组件（比如 `3003` 护盾条）读这个，别去读私有字段。</summary>
    public float Left => _shieldLeft;

    /// <summary>护盾上限。</summary>
    public float Max => Amount;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Amount = JsonTool.GetValue(parameters, "amount", Amount);
        Regen = JsonTool.GetValue(parameters, "regen", Regen);
    }

    /// <summary>装配时接线：满盾开局，然后挂到受伤链的护盾那一格。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，护盾不生效。");
            return;
        }

        _shieldLeft = Amount;

        ball.Events.Register(EventName.take_damage, new EventResponseFunction
        {
            priority = DamagePriority.Shield,
            action = OnTakeDamage,
        });
    }

    /// <summary>受伤链上的处理：护盾先顶，没顶完的伤害继续往后传。</summary>
    public bool OnTakeDamage(object arg)
    {
        if (_ball == null || arg is not DamageEvent hit || hit.Amount <= 0f || _shieldLeft <= 0f)
        {
            return true; // 没盾、或者本来就没有伤害要挡：放行
        }

        float absorbed = Mathf.Min(_shieldLeft, hit.Amount);
        _shieldLeft -= absorbed;
        hit.Amount -= absorbed;

        GD.Print(_shieldLeft > 0f
            ? $"[{Type}] {_ball.Name} 护盾顶了 {absorbed:0.#}，还剩 {_shieldLeft:0.#}"
            : $"[{Type}] {_ball.Name} 的护盾碎了（这一下顶了 {absorbed:0.#}）");

        return true; // 不阻塞：没挡住的那部分照常往后走
    }

    /// <summary>回盾：每秒补 `regen`，补到上限为止。</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (_ball == null || Regen <= 0f || _shieldLeft >= Amount)
        {
            return;
        }

        _shieldLeft = Mathf.Min(Amount, _shieldLeft + Regen * (float)delta);
    }

    /// <summary>
    /// 往盾里灌一笔（**别的组件灌盾用，比如无下限 `3004`**），补到上限为止。
    /// 咒力消耗、什么时候灌、灌多少，都是调用方的事——这里只管把盾加上去，
    /// 所以"无下限"不用再实现一遍护盾的逻辑。
    /// </summary>
    public void Restore(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        _shieldLeft = Mathf.Min(Amount, _shieldLeft + amount);
    }
}
