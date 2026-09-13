using Godot;

/// <summary>
/// 装配：读配置 → 生成小球 → 挂组件 → 摆到场上。
/// 现在所有球都用同一个预制体（NormalBall.tscn），球体贴图在运行时按球自己的目录扫出来，
/// 所以玩家自己往 balls 里加的新球也能显示。
/// </summary>
public static class BallAssembler
{
    private const string BallScene = "res://assets/scene/NormalBall.tscn";

    /// <summary>预制体里两个碰撞形状的节点路径。</summary>
    private const string BodyShapePath = "Shape";
    private const string HitShapePath = "HitArea/Shape";

    /// <summary>预制体里命中圈 / 碰撞圈的默认比例（90 / 75）。只写了碰撞圈时按这个比例跟上。</summary>
    private const float HitRadiusScale = 1.2f;

    /// <summary>蛋的节点组。蛋不是球、不在 balls 组里，要单独找它们（比如结算时清场）。</summary>
    public const string EggsGroup = "eggs";

    public static Ball Build(BallData data, Vector2 position, int group)
    {
        if (data == null)
        {
            return null;
        }

        var scene = GD.Load<PackedScene>(BallScene);
        if (scene == null)
        {
            GD.PushError($"[装配] 找不到球的预制体：{BallScene}");
            return null;
        }

        var ball = scene.Instantiate<Ball>();
        ball.Name = data.Id;
        ball.Id = data.Id;
        ball.DisplayName = data.Name;
        ball.Position = position;
        ball.Group = group;
        ball.MaxHp = data.Hp;
        ball.Hp = data.Hp;

        SetAppearance(ball, data);
        int count = AttachComponents(ball, data.SelfComponents);

        GD.Print($"[装配] {data.Id}（{data.Name}）血量 {data.Hp}，自己的组件 {count} 个");
        return ball;
    }

    /// <summary>把一组组件挂到球上。键是组件编号，值是参数。返回挂上了几个。</summary>
    public static int AttachComponents(Ball ball, Godot.Collections.Dictionary components)
    {
        if (ball == null || components == null)
        {
            return 0;
        }

        int attached = 0;

        foreach (var key in components.Keys)
        {
            if (!int.TryParse(key.ToString(), out int id))
            {
                GD.PushError($"[装配] 组件编号看不懂：{key}");
                continue;
            }

            var component = ComponentLibrary.Create(id);
            if (component == null)
            {
                continue;
            }

            var value = components[key];
            component.ApplyParams(value.VariantType == Variant.Type.Dictionary
                ? value.AsGodotDictionary()
                : new Godot.Collections.Dictionary());

            ball.AddChild(component);
            Wire(ball, component);
            attached++;
        }

        return attached;
    }

    /// <summary>把组件接到球上：该连信号的连信号，该注册事件的注册事件。</summary>
    private static void Wire(Ball ball, BallComponent component)
    {
        switch (component)
        {
            case CollisionAttack attack:
                // 阵营跟着球走：碰到同阵营的球不算敌人
                attack.Group = ball.Group;
                ConnectHitArea(ball, attack.OnBodyEntered);
                break;

            case Shift shift:
                // 屎蛋：阵营跟着球走，检测圈也接上（撞到谁给谁挂减速）
                shift.Group = ball.Group;
                ConnectHitArea(ball, shift.OnBodyEntered);
                break;

            case Poison poison:
                // 中毒：夹在护盾（700）和一般扣血（500）中间，不阻塞，只负责染色
                ball.Events.Register(EventName.take_damage, new EventResponseFunction
                {
                    priority = DamagePriority.DamageOverTime,
                    action = poison.OnTakeDamage,
                });
                break;


            case NormalDamage damage:
                // 受伤链的最后一环：挂到"受伤"事件上，按优先级排进链里
                ball.Events.Register(EventName.take_damage, new EventResponseFunction
                {
                    priority = DamagePriority.NormalDamage,
                    action = damage.OnTakeDamage,
                });
                break;

            case CircleShape shape:
                // 碰撞箱：把球上的圈改成组件要的半径（不写参数就保持预制体的默认圆）
                WireCircleShape(ball, shape);
                break;
        }
    }

    /// <summary>
    /// 圆形碰撞箱：按组件里的半径改球上的碰撞圈和命中圈。
    /// 两个参数都没写就直接返回——默认圆留在预制体里，这里不该动它。
    /// </summary>
    private static void WireCircleShape(Ball ball, CircleShape component)
    {
        if (component.Radius <= 0f && component.HitRadius <= 0f)
        {
            return;
        }

        if (component.Radius > 0f)
        {
            SetCircleRadius(ball, BodyShapePath, component.Radius, "碰撞圈");
        }

        if (component.HitRadius > 0f)
        {
            SetCircleRadius(ball, HitShapePath, component.HitRadius, "命中圈");
        }
        else if (component.Radius > 0f)
        {
            // 只写了碰撞圈：命中圈按预制体里的比例跟上。检测圈必须比球体稍大，
            // 只改碰撞圈会让两个圈的比例走样（等大就永远差一点点触发不了碰撞）。
            SetCircleRadius(ball, HitShapePath, component.Radius * HitRadiusScale, "命中圈");
        }
    }

    /// <summary>
    /// 改一个碰撞圈的半径。
    ///
    /// 改之前必须先复制：预制体里那个 CircleShape2D 是所有球共用的同一个资源
    /// （resource_local_to_scene = false），直接改半径会让场上一屏的球、乃至之后
    /// 每个新球都跟着变。复制一份之后，这颗球才真的有自己的形状。
    /// </summary>
    private static void SetCircleRadius(Ball ball, string shapePath, float radius, string label)
    {
        var node = ball.GetNodeOrNull<CollisionShape2D>(shapePath);
        if (node == null)
        {
            GD.PushError($"[装配] 预制体里找不到{label}节点（{shapePath}），半径没改成功。");
            return;
        }

        if (node.Shape is not CircleShape2D circle)
        {
            GD.PushError($"[装配] {label}不是圆形，shape.circle 改不了它。");
            return;
        }

        circle = (CircleShape2D)circle.Duplicate(); // 复制成这颗球自己的形状
        circle.Radius = radius;
        node.Shape = circle;

        GD.Print($"[装配] {ball.Name} 的{label}半径改成 {radius:0.#}");
    }

    /// <summary>
    /// 产一颗蛋。**蛋不是球**：它就是一个普通节点——一个 `Area2D` 当检测圈，
    /// 长什么样、多大、干什么，全交给 `eggComponentId` 指的那个组件自己搭（见 `2003`）。
    ///
    /// 因为不是球，它不进 `balls` 组、不参与胜负判定、没有血量，也不是实体
    /// （别人碰不到它，只能它碰到别人）。返回 null 表示组件编号不存在。
    /// </summary>
    public static Node2D BuildEgg(Vector2 position, int group, int componentId)
    {
        var component = ComponentLibrary.Create(componentId);
        if (component == null)
        {
            GD.PushError($"[装配] 蛋要挂的组件 {componentId} 不存在，这颗蛋没生成。");
            return null;
        }

        var egg = new Area2D
        {
            Name = $"Egg{componentId}",
            Position = position,
            Monitoring = true,
            CollisionLayer = 0, // 它不是实体：别人不用感知它
            CollisionMask = 1,  // 只去感知球（球在层 1 上）
        };

        egg.AddToGroup(EggsGroup);

        // 空壳在这里搭好：一个图的槽 + 一个检测圈。
        // 必须在进场景树之前搭——进了树之后在组件 _Ready 里 add_child，引擎会以
        // "父节点正在建子节点" 为由拒绝（这个坑踩过两次了）。
        // 换成什么图、圈多大，由组件在自己 _Ready 里填（见 2003）。
        egg.AddChild(new Sprite2D { Name = "Sprite" });
        egg.AddChild(new CollisionShape2D
        {
            Name = "Shape",
            Shape = new CircleShape2D { Radius = 1f },
        });

        // 阵营得让组件知道，不然蛋会对自己人开火
        component.ApplyParams(new Godot.Collections.Dictionary { { "group", group } });

        egg.AddChild(component);
        return egg;
    }

    /// <summary>把球身上的碰撞检测接上：有东西进圈就调传进来的处理函数。</summary>
    private static void ConnectHitArea(Ball ball, Area2D.BodyEnteredEventHandler handler)
    {
        var hitArea = ball.GetNodeOrNull<Area2D>("HitArea");
        if (hitArea == null)
        {
            GD.PushError("[装配] 预制体里找不到 HitArea，攻击接不上，这个球打不到人。");
            return;
        }

        hitArea.BodyEntered += handler;
    }

    /// <summary>外观：`type` 1（或没写）用贴图，2 用 Spine；Spine 凑不齐就回落到贴图。</summary>
    private static void SetAppearance(Ball ball, BallData data)
    {
        if (data.Type == 2 && SetSpineAppearance(ball, data))
        {
            return;
        }

        SetBodyTexture(ball, data.Id);
    }

    /// <summary>
    /// type=2：交给 Spine 显示。成功返回 true；资源不齐、引擎里没有 SpineSprite 之类
    /// 都返回 false，让调用方回落到贴图（新球缺素材时不会变成一团黑）。
    /// </summary>
    private static bool SetSpineAppearance(Ball ball, BallData data)
    {
        var look = new SpineLook { Scale = data.SpineScale };
        if (!look.Prepare(ball))
        {
            return false;
        }

        ball.AddChild(look);
        return true;
    }

    /// <summary>球体贴图按球自己的目录扫出来。</summary>
    private static void SetBodyTexture(Ball ball, string ballId)
    {
        var body = ball.GetNodeOrNull<Sprite2D>("Body");
        if (body == null)
        {
            GD.PushError("[装配] 预制体里找不到 Body 节点，贴图没换。");
            return;
        }

        var texture = BallLibrary.LoadAvatar(UserData.Balls + ballId + "/resource/avatar.png");
        if (texture != null)
        {
            body.Texture = texture;
        }
    }
}
