using Godot;

/// <summary>一个球的完整配置，对应 balldata.json。</summary>
public sealed class BallData
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public float Hp = 100f;
    public int Type = 1;

    /// <summary>自己的组件。键是组件编号，值是参数。</summary>
    public Godot.Collections.Dictionary SelfComponents = new();

    /// <summary>挂到对面身上的组件。</summary>
    public Godot.Collections.Dictionary EnemyComponents = new();

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
            SelfComponents = JsonTool.Get(file, "selfcomponents", new Godot.Collections.Dictionary()),
            EnemyComponents = JsonTool.Get(file, "enemycomponents", new Godot.Collections.Dictionary()),
        };
    }
}
