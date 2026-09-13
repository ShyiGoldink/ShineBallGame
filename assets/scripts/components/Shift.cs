using Godot;

/// <summary>
/// 屎蛋（名字先叫 shift）：**它不是一个球**，就是一个普通节点——
/// 一个 `Area2D`（当检测圈）+ 一张图（`Sprite2D`）。有敌人撞上来就把它切成受控状态
/// （减速的入口），同时开始每秒造成对方最大生命值 1/20 的伤害（单次最多 100 点）；
/// 效果走完自己消失。
///
/// 因为它不是球：**不进 `balls` 组、不参与胜负判定、没有血量**，也不是实体
/// （碰不到它，只能它碰到你）。它长什么样、多大，全写在它自己的参数里。
/// </summary>
public partial class Shift : BallComponent
{
    public override int Id => 2003;

    public override string Type => "attack.shift";

    public override string Description => "蛋：撞到敌人给它减速并每秒跳伤，效果走完自己消失（普通节点，不是球）";

    /// <summary>蛋的素材。`res://` 开头当资源加载，其它路径（user:// 那种）直接读文件。</summary>
    public string Texture = "res://resources/shit.png";

    /// <summary>蛋显示多大（像素，按素材长边算）。</summary>
    public float Size = 60f;

    /// <summary>效果持续几秒：这段时间里对面受控，每秒跳一次伤害。</summary>
    public float Duration = 5f;

    /// <summary>隔几秒跳一次伤害。</summary>
    public float TickInterval = 1f;

    /// <summary>每次跳多少：对方最大生命值 × 这个比例。</summary>
    public float DamageRatio = 0.05f;

    /// <summary>单次伤害上限。</summary>
    public float DamageCap = 100f;

    /// <summary>蛋的阵营，由装配器填（跟下它的那颗球一样）。同阵营撞上来不算敌人。</summary>
    public int Group;

    private Area2D _egg;
    private Ball _target;
    private float _effectLeft;
    private float _tickLeft;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Texture = JsonTool.GetValue(parameters, "texture", Texture);
        Size = JsonTool.GetValue(parameters, "size", Size);
        Duration = JsonTool.GetValue(parameters, "duration", Duration);
        TickInterval = JsonTool.GetValue(parameters, "tick_interval", TickInterval);
        DamageRatio = JsonTool.GetValue(parameters, "damage_ratio", DamageRatio);
        DamageCap = JsonTool.GetValue(parameters, "damage_cap", DamageCap);
        Group = JsonTool.GetValue(parameters, "group", Group);
    }

    public override void _Ready()
    {
        _egg = GetParent() as Area2D;
        if (_egg == null)
        {
            GD.PushError($"[{Type}] 组件没挂在蛋（Area2D）下面，这颗蛋什么都不会做。");
            return;
        }

        BuildLook();

        // 蛋自己接线：装配器那套 Wire 是给球用的，蛋不走那边
        _egg.BodyEntered += OnBodyEntered;
    }

    /// <summary>
    /// 把装配器搭好的空壳填上：`Sprite` 槽换成蛋的图并缩到 Size，`Shape` 槽配成检测圈。
    /// 这里只改现成的子节点、不新建——在 _Ready 里 add_child 会被引擎拒绝（父节点正在建子节点）。
    /// </summary>
    private void BuildLook()
    {
        var sprite = _egg.GetNodeOrNull<Sprite2D>("Sprite");
        var texture = LoadTexture();

        if (texture == null)
        {
            GD.PushWarning($"[{Type}] 找不到蛋的素材：{Texture}");
        }
        else if (sprite == null)
        {
            GD.PushWarning($"[{Type}] 蛋节点里没有 Sprite 槽，没换上皮。");
        }
        else
        {
            sprite.Texture = texture;

            // 按长边缩到 Size：素材不一定正好是方的，缩完最长的边等于 Size
            float longest = Mathf.Max(texture.GetWidth(), texture.GetHeight());
            sprite.Scale = Vector2.One * (Size / longest);
        }

        // 检测圈：显示大小的一半，再按 1.2 倍放宽（跟球身上那两个圈一个比例）。
        // 这个形状是装配器新建的、这颗蛋独占，所以直接改半径就行，不用先复制
        // （预制体里那种共用的形状才需要复制）。
        if (_egg.GetNodeOrNull<CollisionShape2D>("Shape")?.Shape is CircleShape2D circle)
        {
            circle.Radius = Size * 0.5f * 1.2f;
        }
    }

    private Texture2D LoadTexture()
    {
        if (string.IsNullOrEmpty(Texture))
        {
            return null;
        }

        // 工程里的资源走加载器；user:// 下的文件没进过导入流程，只能直接读图
        return Texture.StartsWith("res://") ? GD.Load<Texture2D>(Texture) : BallLibrary.LoadAvatar(Texture);
    }

    /// <summary>有东西撞上来：是敌人的话就开打（阵营一样、或者不是球，都不算）。</summary>
    public void OnBodyEntered(Node2D body)
    {
        if (body is not Ball enemy || enemy.Group == Group)
        {
            return;
        }

        // 撞上就立刻疼一下，之后每秒一次；重复撞到把时间重新计时，不会叠加
        _target = enemy;
        _effectLeft = Duration;
        _tickLeft = 0f;

        // 撞上就"把壳收掉"：缩放设成 0 就等于看不见了。
        // 注意别在这里 QueueFree——效果还要靠这个节点跑 5 秒（它得活着才能继续跳伤）。
        _egg.Scale = Vector2.Zero;

        // 切成受控状态：对面挂着控制类组件就由那边接手移动（减速），没挂就是定身
        _target.BeControlled(Duration, ControlRepeat.Reset);

        GD.Print($"[{Type}] {_egg.Name} 让 {enemy.Name} 受控 {Duration} 秒，并开始每秒跳伤");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_egg == null || _target == null)
        {
            return;
        }

        // 目标没了（死了 / 被清掉了）就收工，蛋也一起消失
        if (!IsInstanceValid(_target) || _target.State == BallState.Dead)
        {
            Finish();
            return;
        }

        _effectLeft -= (float)delta;
        if (_effectLeft <= 0f)
        {
            Finish();
            return;
        }

        _tickLeft -= (float)delta;
        if (_tickLeft > 0f)
        {
            return;
        }

        _tickLeft = TickInterval;

        // 伤害按"对面最大生命值的比例"算，再卡上限
        float damage = Mathf.Min(_target.MaxHp * DamageRatio, DamageCap);
        GD.Print($"[{Type}] {_target.Name} 中毒跳伤 {damage:0.#}");
        _target.TakeDamage(damage);
    }

    /// <summary>
    /// 效果走完（或者目标没了）：这颗蛋没用了，自己消失。
    ///
    /// 生命周期注意两点：
    /// 1. 用 `QueueFree` 而不是 `Free`——触发是在 `Area2D.body_entered` 的信号回调里跑的，
    ///    当场删节点会把正在派发信号的那套东西一起掀掉；QueueFree 排到本帧末尾再删。
    /// 2. 删的是这颗蛋；减速（6001）和中毒染色（4002）挂在**对面身上**，不归它管，
    ///    所以蛋消失不会把效果一起带走。
    /// </summary>
    private void Finish()
    {
        _target = null;
        _effectLeft = 0f;

        if (_egg != null && IsInstanceValid(_egg))
        {
            GD.Print($"[{Type}] {_egg.Name} 效果结束，蛋消失");
            _egg.QueueFree();
        }
    }
}
