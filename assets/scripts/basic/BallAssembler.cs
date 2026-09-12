using Godot;

/// <summary>
/// 装配：读配置 → 生成小球 → 挂组件 → 摆到场上。
/// 现在所有球都用同一个预制体（NormalBall.tscn），球体贴图在运行时按球自己的目录扫出来，
/// 所以玩家自己往 balls 里加的新球也能显示。
/// </summary>
public static class BallAssembler
{
    private const string BallScene = "res://assets/scene/NormalBall.tscn";

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
        ball.Position = position;
        ball.Group = group;
        ball.MaxHp = data.Hp;
        ball.Hp = data.Hp;

        SetBodyTexture(ball, data.Id);
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
                ConnectHitArea(ball, attack);
                break;

            case NormalDamage damage:
                // 受伤链的最后一环：挂到"受伤"事件上，按优先级排进链里
                ball.Events.Register(EventName.take_damage, new EventResponseFunction
                {
                    priority = DamagePriority.NormalDamage,
                    action = damage.OnTakeDamage,
                });
                break;
        }
    }

    /// <summary>把球身上的碰撞检测接到碰撞攻击上：有东西进圈就调它的处理函数。</summary>
    private static void ConnectHitArea(Ball ball, CollisionAttack attack)
    {
        var hitArea = ball.GetNodeOrNull<Area2D>("HitArea");
        if (hitArea == null)
        {
            GD.PushError("[装配] 预制体里找不到 HitArea，碰撞攻击接不上，这个球打不到人。");
            return;
        }

        hitArea.BodyEntered += attack.OnBodyEntered;
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
