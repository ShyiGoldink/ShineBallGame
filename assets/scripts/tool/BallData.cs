using Godot;

/// <summary>一个球的完整配置，对应 balldata.json。</summary>
public sealed class BallData
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public float Hp = 100f;
    public int Type = 1;

    /// <summary>Spine 骨架的额外缩放（type=2 用）。想让角色在场上占 150px，就配 150 ÷ 角色在 Spine 里的高度。</summary>
    public float SpineScale = 1f;

    /// <summary>自己的组件。键是组件编号，值是参数。</summary>
    public Godot.Collections.Dictionary SelfComponents = new();

    /// <summary>挂到对面身上的组件。</summary>
    public Godot.Collections.Dictionary EnemyComponents = new();

    /// <summary>
    /// 招式表：招式名 → `{ "time": 演多久, "anim": 配哪段动画 }`（可以没有，可以只写一部分）。
    /// 读法是 `MoveTable`，写法在 `FunctionGuide` 的招式那一节。
    /// </summary>
    public Godot.Collections.Dictionary Attacks = new();

    public static BallData Load(string ballId)
    {
        if (string.IsNullOrEmpty(ballId))
        {
            return null;
        }

        var file = UserData.Balls + ballId + "/balldata.json";
        if (!FileAccess.FileExists(file))
        {
            GD.PushError($"[装配] 找不到球的配置：{file}");
            return null;
        }

        return new BallData
        {
            Id = ballId,
            Name = JsonTool.Get(file, "name", ballId),
            Hp = JsonTool.Get(file, "hp", 100f),
            Type = JsonTool.Get(file, "type", 1),
            // 只有 Spine 球才读这个，不然贴图球会平白多一条"缺 key"的警告
            SpineScale = JsonTool.Get(file, "type", 1) == 2 ? JsonTool.Get(file, "spine_scale", 1f) : 1f,
            SelfComponents = JsonTool.Get(file, "selfcomponents", new Godot.Collections.Dictionary()),
            EnemyComponents = JsonTool.Get(file, "enemycomponents", new Godot.Collections.Dictionary()),
            Attacks = JsonTool.Get(file, "attacks", new Godot.Collections.Dictionary()),
        };
    }
}
