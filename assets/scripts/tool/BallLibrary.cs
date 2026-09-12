using Godot;
using System.Collections.Generic;

/// <summary>扫到的一个小球。</summary>
public sealed class BallEntry
{
    /// <summary>文件夹名，比如 NormalBall，当球的 id 用。</summary>
    public string Id;

    /// <summary>balldata.json 里的 name。</summary>
    public string Name;

    /// <summary>Resources/avatar.png，没有就是 null。</summary>
    public Texture2D Avatar;
}

/// <summary>
/// 扫描 user://balls：一个文件夹算一个球，文件夹里必须有 balldata.json，
/// 头像按约定放在 resource/avatar.png。
/// </summary>
public static class BallLibrary
{
    public static List<BallEntry> LoadAll()
    {
        var balls = new List<BallEntry>();

        if (!DirAccess.DirExistsAbsolute(UserData.Balls))
        {
            GD.PushError($"[小球] 找不到目录：{UserData.Balls}");
            return balls;
        }

        foreach (var dir in DirAccess.GetDirectoriesAt(UserData.Balls))
        {
            if (dir.StartsWith("."))
            {
                continue;
            }

            var folder = UserData.Balls + dir + "/";
            var dataFile = folder + "balldata.json";
            if (!FileAccess.FileExists(dataFile))
            {
                GD.PushWarning($"[小球] {dir} 里没有 balldata.json，跳过。");
                continue;
            }

            balls.Add(new BallEntry
            {
                Id = dir,
                Name = JsonTool.Get(dataFile, "name", dir),
                Avatar = LoadAvatar(folder + "resource/avatar.png"),
            });
        }

        return balls;
    }

    /// <summary>
    /// 头像是 user:// 下的普通 png，没经过 Godot 的导入流程，
    /// 所以不能当资源加载，直接用 Image 读出来做成纹理。
    /// </summary>
    public static Texture2D LoadAvatar(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            GD.PushWarning($"[小球] 缺少头像：{path}");
            return null;
        }

        var image = Image.LoadFromFile(path);
        return image == null ? null : ImageTexture.CreateFromImage(image);
    }
}
