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

    /// <summary>
    /// 在球自己的包里找一个资源：`user://balls/&lt;球id&gt;/resource/&lt;名字&gt;`。
    ///
    /// 名字**可以不写后缀**——会按 `extensions` 依次试；还找不到就去 `user://` 根目录
    /// 找同名文件（少数公共资源放那儿）。写了 `user://` / `res://` 完整路径则原样用。
    /// 找不到返回 null。
    ///
    /// 这是**所有小球独特资源的统一找法**：头像、音效、Spine 三件套、蛋的素材都走它，
    /// 规矩只有一条——"东西放在球自己的 `resource/` 目录里"。
    /// </summary>
    public static string Find(string name, string ballId, params string[] extensions)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (name.Contains("://"))
        {
            return FileAccess.FileExists(name) ? name : null;
        }

        foreach (var candidate in Candidates(name, extensions))
        {
            if (!string.IsNullOrEmpty(ballId))
            {
                var inBall = UserData.Balls + ballId + "/resource/" + candidate;
                if (FileAccess.FileExists(inBall))
                {
                    return inBall;
                }
            }

            var inUser = UserData.Root + candidate;
            if (FileAccess.FileExists(inUser))
            {
                return inUser;
            }
        }

        return null;
    }

    /// <summary>名字本身，外加（没写后缀时）补上几种常见后缀的变体。</summary>
    private static IEnumerable<string> Candidates(string name, string[] extensions)
    {
        yield return name;

        if (string.IsNullOrEmpty(System.IO.Path.GetExtension(name)))
        {
            foreach (var extension in extensions)
            {
                yield return name + extension;
            }
        }
    }
}
