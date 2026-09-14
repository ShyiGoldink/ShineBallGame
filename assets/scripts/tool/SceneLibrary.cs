using Godot;
using System.Collections.Generic;

/// <summary>扫到的一个场地。</summary>
public sealed class SceneEntry
{
    /// <summary>文件夹名，比如 basic，当场地 id 用。</summary>
    public string Id;

    /// <summary>scenedata.json 里的 name。</summary>
    public string Name;

    /// <summary>选择界面用的图：`avatar.png`，没有就是 null。</summary>
    public Texture2D Avatar;
}

/// <summary>
/// 扫描 `user://scenes`：一个文件夹算一个场地，里面要有 `scenedata.json`，
/// 头像按约定放 `avatar.png`。和数据/球是同一套规矩（`DataSeeder` 会把仓库里的默认值补过来）。
/// </summary>
public static class SceneLibrary
{
    public static List<SceneEntry> LoadAll()
    {
        var scenes = new List<SceneEntry>();

        if (!DirAccess.DirExistsAbsolute(UserData.Scenes))
        {
            GD.PushError($"[场地] 找不到目录：{UserData.Scenes}");
            return scenes;
        }

        foreach (var dir in DirAccess.GetDirectoriesAt(UserData.Scenes))
        {
            if (dir.StartsWith("."))
            {
                continue;
            }

            var folder = UserData.Scenes + dir + "/";
            if (!FileAccess.FileExists(folder + "scenedata.json"))
            {
                GD.PushWarning($"[场地] {dir} 里没有 scenedata.json，跳过。");
                continue;
            }

            scenes.Add(new SceneEntry
            {
                Id = dir,
                Name = JsonTool.Get(folder + "scenedata.json", "name", dir),
                Avatar = BallLibrary.LoadAvatar(folder + "avatar.png"),
            });
        }

        return scenes;
    }

    /// <summary>没选过场地时用哪个：列表里的第一个（按名字排序，保证每次一样）。</summary>
    public static string DefaultId()
    {
        var all = LoadAll();
        all.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return all.Count > 0 ? all[0].Id : string.Empty;
    }
}
