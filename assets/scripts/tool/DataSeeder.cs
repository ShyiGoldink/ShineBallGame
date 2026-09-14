using Godot;

/// <summary>
/// 首次运行时，把仓库里自带的默认数据补到 user:// 下。
///
/// 规则是"只补缺，不覆盖"：玩家 / 作者改过的文件永远不会被冲掉，
/// 新版本新增的默认文件则会自动补上。手机端也只能靠这个办法拿到初始数据。
/// </summary>
public static class DataSeeder
{
    /// <summary>仓库里的默认数据目录，结构和 user:// 一一对应。</summary>
    private const string DefaultRoot = "res://assets/data/";

    public static void SeedMissing()
    {
        UserData.EnsureFolders();
        CopyMissing(DefaultRoot + "balls/", UserData.Balls);
        CopyMissing(DefaultRoot + "scene/", UserData.Scene);
        CopyMissing(DefaultRoot + "scenes/", UserData.Scenes);
    }

    private static void CopyMissing(string source, string target)
    {
        if (!DirAccess.DirExistsAbsolute(source))
        {
            GD.PushWarning($"[数据] 默认数据目录不存在：{source}");
            return;
        }

        // 先补文件：目标已经有同名文件就不动它
        foreach (var file in DirAccess.GetFilesAt(source))
        {
            // 跳过 Godot 自己的导入文件和其他隐藏文件
            if (file.StartsWith(".") || file.EndsWith(".import"))
            {
                continue;
            }

            var targetFile = target + file;
            if (FileAccess.FileExists(targetFile))
            {
                continue;
            }

            var error = DirAccess.CopyAbsolute(source + file, targetFile);
            if (error == Error.Ok)
            {
                GD.Print($"[数据] 补上默认文件 {file} → {UserData.Global(targetFile)}");
            }
            else
            {
                GD.PushError($"[数据] 复制失败：{file}（{error}）");
            }
        }

        // 再进子文件夹：目标没有就先建出来，然后递归进去补里面的东西。
        // 球是一个文件夹一个球（比如 NormalBall/balldata.json），所以必须递归。
        foreach (var dir in DirAccess.GetDirectoriesAt(source))
        {
            if (dir.StartsWith("."))
            {
                continue;
            }

            var targetDir = target + dir + "/";
            if (!DirAccess.DirExistsAbsolute(targetDir))
            {
                var error = DirAccess.MakeDirRecursiveAbsolute(targetDir);
                if (error != Error.Ok)
                {
                    GD.PushError($"[数据] 建目录失败：{targetDir}（{error}）");
                    continue;
                }

                GD.Print($"[数据] 新建文件夹 {UserData.Global(targetDir)}");
            }

            CopyMissing(source + dir + "/", targetDir);
        }
    }
}
