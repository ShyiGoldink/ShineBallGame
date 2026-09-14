using Godot;

/// <summary>
/// user:// 下的数据目录布局。Json 数据都放在这里，方便作者和玩家直接改文件。
/// 目录不在仓库里，而是启动时创建，这样手机端也不会因为找不到文件夹而报错。
///
/// 想打开看的话：UserData.Global(UserData.Balls) 就是系统里的真实路径。
/// </summary>
public static class UserData
{
    public const string Root = "user://";

    /// <summary>场景参数：场地大小、墙、出生点之类，之后用。</summary>
    public const string Scene = Root + "scene/";

    /// <summary>小球数据：一个球一个 Json 文件。</summary>
    public const string Balls = Root + "balls/";

    /// <summary>场地数据：一个场地一个文件夹（`scenedata.json` + `avatar.png`）。</summary>
    public const string Scenes = Root + "scenes/";

    /// <summary>创建所有数据目录。重复调用无害。</summary>
    public static void EnsureFolders()
    {
        Make(Root);
        Make(Scene);
        Make(Balls);
        Make(Scenes);
    }

    /// <summary>把 user:// 路径换成系统里的真实路径。</summary>
    public static string Global(string path) => ProjectSettings.GlobalizePath(path);

    private static void Make(string path)
    {
        if (DirAccess.DirExistsAbsolute(path))
        {
            return;
        }

        var error = DirAccess.MakeDirRecursiveAbsolute(path);
        if (error != Error.Ok)
        {
            GD.PushError($"[数据] 创建目录失败：{path}（{error}）");
            return;
        }

        GD.Print($"[数据] 已创建目录：{Global(path)}");
    }
}
