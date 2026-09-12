using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 通用 Json 读取工具：从哪个文件，取哪个 key 的值。
///
/// 用法：
///     var hp       = JsonTool.Get("balls/spark.json", "stats.hp", 100f);
///     var name     = JsonTool.Get("balls/spark.json", "name", "无名球");
///     var list     = JsonTool.Get("balls/spark.json", "components", new Godot.Collections.Array());
///     var enemyPos = JsonTool.Get("scene/arena.json", "spawn.enemy", Vector2.Zero);
///
/// 说明：
/// - 相对路径会自动补上 user:// 前缀，所以 "balls/spark.json" 就是 user://balls/spark.json；
///   写 "res://..." 这种带协议头的路径则原样使用。
/// - key 支持用点号往对象里钻，例如 "stats.hp"；数组整段读出来自己遍历，点号不解析下标。
/// - 同一个文件只会解析一次，之后走缓存；改完文件调 JsonTool.Reload() 重新读；
/// - 取不到时返回 fallback，并且会报出来（缺文件 / 缺 key / 类型不对的提示各不一样）。
///
/// 泛型上的 [MustBeVariant] 是 Godot 4.7 的要求：T 必须是 Variant 能表达的类型
/// （int / float / string / Vector2 / Color / Dictionary / Array …），
/// 传别的类型会在编译期就报错，等于顺手帮你挡掉一类用法错误。
/// </summary>
public static class JsonTool
{
    private static readonly Dictionary<string, Godot.Collections.Dictionary> Cache = new();

    /// <summary>从文件里取 key 的值，取不到或类型不对就返回 fallback。</summary>
    public static T Get<[MustBeVariant] T>(string file, string key, T fallback = default)
    {
        return TryGet(file, key, out T value) ? value : fallback;
    }

    /// <summary>取值的"可能失败"版本，想知道到底有没有取到时用它。</summary>
    public static bool TryGet<[MustBeVariant] T>(string file, string key, out T value)
    {
        value = default;

        var root = Load(file);
        if (root == null)
        {
            return false;
        }

        if (!TryFind(root, key, out var variant))
        {
            GD.PushWarning($"[Json] {Resolve(file)} 里没有 '{key}'，用默认值代替。");
            return false;
        }

        if (!TryConvert(variant, out value))
        {
            GD.PushError($"[Json] {Resolve(file)} 的 '{key}' 不是 {typeof(T).Name} 类型，用默认值代替。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 从一个已经读进来的对象里取值，给组件读自己的参数用。
    /// 取不到就返回 fallback —— 参数是可选的，缺省就是走组件自己的默认值。
    /// </summary>
    public static T GetValue<[MustBeVariant] T>(Godot.Collections.Dictionary source, string key, T fallback = default)
    {
        if (source == null || !TryFind(source, key, out var variant) || !TryConvert(variant, out T value))
        {
            return fallback;
        }

        return value;
    }

    /// <summary>读出一整个 Json 文件（最外层必须是一个对象）。</summary>
    public static Godot.Collections.Dictionary Load(string file)
    {
        var path = Resolve(file);
        if (path == null)
        {
            return null;
        }

        if (Cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        if (!FileAccess.FileExists(path))
        {
            GD.PushError($"[Json] 找不到文件：{path}");
            return null;
        }

        using var handle = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (handle == null)
        {
            GD.PushError($"[Json] 打不开文件：{path}（{FileAccess.GetOpenError()}）");
            return null;
        }

        var json = new Json();
        var error = json.Parse(handle.GetAsText());
        if (error != Error.Ok)
        {
            GD.PushError($"[Json] {path} 解析失败：第 {json.GetErrorLine()} 行 —— {json.GetErrorMessage()}");
            return null;
        }

        if (json.Data.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError($"[Json] {path} 的最外层必须是对象 {{...}}。");
            return null;
        }

        var root = json.Data.AsGodotDictionary();
        Cache[path] = root;
        return root;
    }

    /// <summary>文件存不存在（用于列出有哪些小球之类的场合）。</summary>
    public static bool Exists(string file)
    {
        var path = Resolve(file);
        return path != null && FileAccess.FileExists(path);
    }

    /// <summary>丢掉缓存，下次读取时重新解析。传 file 只清这一个文件。</summary>
    public static void Reload(string file = null)
    {
        if (file == null)
        {
            Cache.Clear();
            return;
        }

        var path = Resolve(file);
        if (path != null)
        {
            Cache.Remove(path);
        }
    }

    /// <summary>相对路径补 user:// 前缀；带协议头的路径原样返回。</summary>
    private static string Resolve(string file)
    {
        if (string.IsNullOrEmpty(file))
        {
            GD.PushError("[Json] 文件路径为空。");
            return null;
        }

        return file.Contains("://") ? file : UserData.Root + file;
    }

    /// <summary>按点号逐层往里找，例如 "stats.hp"。</summary>
    private static bool TryFind(Godot.Collections.Dictionary root, string key, out Variant value)
    {
        value = default;

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var parts = key.Split('.');
        var current = root;

        for (int i = 0; i < parts.Length; i++)
        {
            if (!current.ContainsKey(parts[i]))
            {
                return false;
            }

            var found = current[parts[i]];
            if (i == parts.Length - 1)
            {
                value = found;
                return true;
            }

            if (found.VariantType != Variant.Type.Dictionary)
            {
                return false;
            }

            current = found.AsGodotDictionary();
        }

        return false;
    }

    /// <summary>
    /// Variant 转成调用者要的类型。常用类型直接转（Json 里没有小数的数字也能当 int 读），
    /// 其余交给 Godot 自己的 As&lt;T&gt;，失败就返回 false 让上层用默认值。
    /// </summary>
    private static bool TryConvert<[MustBeVariant] T>(Variant variant, out T result)
    {
        result = default;

        try
        {
            var type = typeof(T);

            if (type == typeof(bool))
            {
                result = (T)(object)variant.AsBool();
            }
            else if (type == typeof(int))
            {
                result = (T)(object)(int)variant.AsInt64();
            }
            else if (type == typeof(long))
            {
                result = (T)(object)variant.AsInt64();
            }
            else if (type == typeof(float))
            {
                result = (T)(object)(float)variant.AsDouble();
            }
            else if (type == typeof(double))
            {
                result = (T)(object)variant.AsDouble();
            }
            else if (type == typeof(string))
            {
                result = (T)(object)variant.AsString();
            }
            else if (type == typeof(Vector2))
            {
                result = (T)(object)variant.AsVector2();
            }
            else if (type == typeof(Color))
            {
                result = (T)(object)variant.AsColor();
            }
            else if (type == typeof(Godot.Collections.Dictionary))
            {
                result = (T)(object)variant.AsGodotDictionary();
            }
            else if (type == typeof(Godot.Collections.Array))
            {
                result = (T)(object)variant.AsGodotArray();
            }
            else
            {
                result = variant.As<T>();
            }

            return true;
        }
        catch (Exception)
        {
            result = default;
            return false;
        }
    }
}
