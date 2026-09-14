using Godot;
using System.Collections.Generic;

/// <summary>
/// 装配中心：把"数据"变成"场上的一颗球"。这里只做三件事，而且**不认识任何具体组件**：
///
/// 1. `Build` —— 建球、摆外观（交给 `BallLook`）、挂组件、摆血条；
/// 2. `AttachComponents` —— 按编号建组件、填参数、挂到球下面、调组件的 `Bind`；
/// 3. `BuildEgg` —— 产一颗蛋（**蛋不是球**，见 `Egg` / `EggLibrary`）。
///
/// 为什么这里不认识组件：每个组件自己知道要接什么线（注册事件、连信号、改形状都写在它的
/// `Bind` 里），所以**加组件不用动这个文件**。"编号 → 组件"的翻译表在 `ComponentLibrary`，
/// "编号 → 蛋"在 `EggLibrary`。
/// </summary>
public static class BallAssembler
{
    private const string BallScene = "res://assets/scene/NormalBall.tscn";

    /// <summary>蛋的节点组。蛋不是球、不在 balls 组里，要单独找（比如结算时清场）。</summary>
    public const string EggsGroup = "eggs";

    /// <summary>
    /// 同一种球 / 蛋的实例计数，用来给节点起名：`pulipuli#1`、`pulipuli#2`……
    /// 不编号的话引擎会把重名节点改成 `@CharacterBody2D@42` 那种，日志里没法看。
    /// 注意：名字只是给人和日志看的，**数据身份是 `Ball.Id`**（不带序号的那个）。
    /// </summary>
    private static readonly Dictionary<string, int> SerialByBase = new();

    /// <summary>装配一颗球：数据 + 位置 + 阵营 → 一个还没进场景树的球节点。</summary>
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
        ball.Name = Numbered(data.Id); // 名字带序号，只给日志看
        ball.Id = data.Id;             // id 才是数据身份（找素材、找音效都用它）
        ball.DisplayName = data.Name;
        ball.Position = position;
        ball.Group = group;
        ball.MaxHp = data.Hp;
        ball.Hp = data.Hp;

        BallLook.Apply(ball, data); // 外观：type=2 走 Spine，其它走贴图；Spine 缺东西自动回落
        int count = AttachComponents(ball, data.SelfComponents, data.Id);

        // 血条等所有组件挂完再摆：5001 可能刚改过碰撞圈，位置得按最终半径算
        BallLook.PlaceHealthBar(ball);

        GD.Print($"[装配] {ball.Name}（{data.Name}）血量 {data.Hp}，自己的组件 {count} 个");
        return ball;
    }

    /// <summary>
    /// 把一组组件挂到球上。键是组件编号，值是参数；返回挂上了几个。
    ///
    /// `configId` 是"哪颗球的 Json 配出了这些组件"：自己身上的是这颗球，
    /// 从 `enemycomponents` 挂过来的则是**配它的那颗球**——组件找素材、找音效时用它，
    /// 而不是用现在挂着的这颗（否则"我给你的攻击"会去你的包里找音效）。
    /// </summary>
    public static int AttachComponents(Ball ball, Godot.Collections.Dictionary components, string configId = null)
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
            component.Bind(ball, configId); // 组件自己接线；装配器不碰事件、不认类型
            attached++;
        }

        return attached;
    }

    /// <summary>
    /// 产一颗蛋。**蛋不是球**：它就是个普通节点（`Area2D` + 一张图），
    /// 长什么样、干什么，全交给 `EggLibrary` 里那个编号对应的蛋类。
    ///
    /// `ownerId` 是下蛋那颗球的 id：蛋用它找素材、也用它在伤害事件里记"谁下的蛋"。
    /// </summary>
    public static Egg BuildEgg(Vector2 position, int group, int eggId, string ownerId)
    {
        var egg = EggLibrary.Create(eggId);
        if (egg == null)
        {
            GD.PushError($"[装配] 没有编号为 {eggId} 的蛋，这颗蛋没生成。");
            return null;
        }

        egg.Name = Numbered($"Egg{eggId}");
        egg.Position = position;
        egg.SetOwner(group, ownerId); // 搭好图和检测圈（必须在进场景树之前）

        return egg;
    }

    /// <summary>给同一种东西起带序号的名字。</summary>
    private static string Numbered(string baseName)
    {
        SerialByBase.TryGetValue(baseName, out int serial);
        serial++;
        SerialByBase[baseName] = serial;
        return $"{baseName}#{serial}";
    }
}
