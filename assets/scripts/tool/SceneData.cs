using Godot;

/// <summary>
/// 一个**场地**的配置，对应 `user://scenes/&lt;场地id&gt;/scenedata.json`。
///
/// 现在场地数据只有一样东西：**出生范围**——两颗球各自可以随机站在哪块矩形里。
/// 场地本身的墙、边框不在这里，而是预制体：约定放在 `res://assets/scene/arenas/&lt;场地id&gt;.tscn`
/// （id 对上就行，不用在 Json 里写路径，少一份会写错的东西）。
/// </summary>
public sealed class SceneData
{
    public string Id = string.Empty;
    public string Name = string.Empty;

    /// <summary>玩家 1（左边）的出生范围。</summary>
    public Rect2 LeftSpawn = new Rect2(420f, 300f, 100f, 480f);

    /// <summary>玩家 2（右边）的出生范围。</summary>
    public Rect2 RightSpawn = new Rect2(1400f, 300f, 100f, 480f);

    /// <summary>场地预制体的路径（按 id 约定，不写进 Json）。</summary>
    public string PrefabPath => $"res://assets/scene/arenas/{Id}.tscn";

    public static SceneData Load(string sceneId)
    {
        if (string.IsNullOrEmpty(sceneId))
        {
            return null;
        }

        var file = UserData.Scenes + sceneId + "/scenedata.json";
        if (!FileAccess.FileExists(file))
        {
            GD.PushError($"[场地] 找不到场地配置：{file}");
            return null;
        }

        var data = new SceneData
        {
            Id = sceneId,
            Name = JsonTool.Get(file, "name", sceneId),
        };

        data.LeftSpawn = ReadRange(file, "spawn.left", data.LeftSpawn);
        data.RightSpawn = ReadRange(file, "spawn.right", data.RightSpawn);
        return data;
    }

    /// <summary>在出生范围里随机挑一个点。</summary>
    public Vector2 PickSpawn(Rect2 range)
    {
        if (range.Size.X <= 0f || range.Size.Y <= 0f)
        {
            return range.Position; // 范围写成了 0 宽高，就当作"固定点"
        }

        return new Vector2(
            (float)GD.RandRange(range.Position.X, range.Position.X + range.Size.X),
            (float)GD.RandRange(range.Position.Y, range.Position.Y + range.Size.Y));
    }

    /// <summary>读 `[x, y, 宽, 高]` 这种四元组；写得不全就用默认范围。</summary>
    private static Rect2 ReadRange(string file, string key, Rect2 fallback)
    {
        var array = JsonTool.Get(file, key, new Godot.Collections.Array());
        if (array.Count < 4)
        {
            GD.PushWarning($"[场地] {file} 的 {key} 应该是 [x, y, 宽, 高]，先按默认值走。");
            return fallback;
        }

        return new Rect2(array[0].AsSingle(), array[1].AsSingle(), array[2].AsSingle(), array[3].AsSingle());
    }
}
