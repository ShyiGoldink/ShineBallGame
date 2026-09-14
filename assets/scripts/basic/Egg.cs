using Godot;

/// <summary>
/// 蛋的基类。**蛋不是球**：它是一个普通节点（`Area2D` 当检测圈 + 一张图），
/// 撞到敌人就做自己的事，做完自己消失。
///
/// 为什么不走数据配置：蛋和蛋之间的差别可能非常大（有的会自己动、有的会炸、
/// 有的会孵出小兵），用 Json 拼参数反而束手束脚。所以**一种蛋 = 一个子类**，
/// 在 `OnHit` 里写它自己的逻辑；"编号 → 蛋类"的翻译在 `EggLibrary`。
///
/// 因为它不是球：不进 `balls` 组、不参与胜负判定、没有血量，也不是实体
/// （别人碰不到它，只有它感知别人）。
/// </summary>
public abstract partial class Egg : Area2D
{
    /// <summary>下蛋那颗球的阵营。同阵营撞上来不算敌人。</summary>
    public int OwnerGroup { get; private set; }

    /// <summary>下蛋那颗球的 id。找素材、记伤害来源都用它。</summary>
    public string OwnerId { get; private set; } = string.Empty;

    /// <summary>类型名，会写进伤害事件的来源里（"这是什么打来的"）。子类覆盖。</summary>
    public virtual string Type => "egg";

    /// <summary>自己的素材名（不用写后缀）。默认在**下蛋那颗球的** resource 目录里找。</summary>
    protected virtual string Texture => "shit";

    /// <summary>蛋显示多大（像素，按素材长边算）。检测圈按它的一半 × 1.2 算。</summary>
    protected virtual float Size => 60f;

    /// <summary>
    /// 装配时调（**必须在进场景树之前**）：记住出身，并把图和检测圈搭好。
    /// 进树之后再 add_child 会被引擎拒绝（"Parent node is busy setting up children"）。
    /// </summary>
    public void SetOwner(int group, string ownerId)
    {
        OwnerGroup = group;
        OwnerId = ownerId;

        BuildSprite();
        BuildShape();
        AddToGroup(BallAssembler.EggsGroup);
    }

    public override void _Ready()
    {
        // 撞到人：先在这里筛掉"不是球"和"自己人"，再交给子类
        BodyEntered += OnBodyEntered;
    }

    /// <summary>撞到敌人时干什么——子类实现这个。</summary>
    protected abstract void OnHit(Ball enemy);

    /// <summary>
    /// 把壳收掉（缩放设 0，肉眼就是没了）。**这里别删节点**：触发是在
    /// `Area2D.body_entered` 的回调里跑的，当场删会把正在派发信号的那套东西一起掀掉；
    /// 而且很多蛋"消失"之后还有后续要做（跳伤、爆炸动画），得留着节点干活。
    /// </summary>
    protected void HideShell()
    {
        Scale = Vector2.Zero;
    }

    /// <summary>收工：排到本帧末尾删掉自己。</summary>
    protected void Destroy()
    {
        QueueFree();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Ball enemy || enemy.Group == OwnerGroup)
        {
            return; // 不是球、或者自己人，都不算
        }

        OnHit(enemy);
    }

    /// <summary>挂一张图。素材在下蛋那颗球的包里找（和音效同一套规则）。</summary>
    private void BuildSprite()
    {
        var sprite = new Sprite2D { Name = "Sprite" };

        var path = BallLibrary.Find(Texture, OwnerId, ".png");
        if (path == null)
        {
            GD.PushWarning($"[蛋] 找不到素材：{Texture}（在下蛋那颗球的 resource 里找过了）");
        }
        else
        {
            var texture = BallLibrary.LoadAvatar(path);
            sprite.Texture = texture;
            BallLook.FitSprite(sprite, texture, Size);
        }

        AddChild(sprite);
    }

    /// <summary>检测圈：半径 = 显示大小的一半 × 1.2（和球身上那两个圈一个比例）。</summary>
    private void BuildShape()
    {
        AddChild(new CollisionShape2D
        {
            Name = "Shape",
            Shape = new CircleShape2D { Radius = Size * 0.5f * BallLook.HitRadiusScale },
        });
    }
}
