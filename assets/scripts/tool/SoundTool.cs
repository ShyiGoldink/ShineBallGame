using Godot;
using System.Collections.Generic;

/// <summary>
/// 音效：从 user:// 里读音频文件，放一遍。
///
/// 路径怎么找（组件里 `sound` 配置的值）：
///   1. 带协议头的（`user://` / `res://`）原样用；
///   2. 不带协议头的，先找这颗球自己的目录：user://balls/&lt;球id&gt;/resource/&lt;值&gt;；
///   3. 还找不到就当成 user:// 下的相对路径：user://&lt;值&gt;。
///
/// 认的后缀：`.wav` / `.ogg` / `.mp3`。这些文件不走 Godot 的导入流程，
/// 所以用引擎自带的 load_from_file 直接读文件，不经过 ResourceLoader
/// （跟头像 avatar.png 一个道理）。
///
/// 同一个文件只加载一次（缓存）；播放器挂在调用者身上，一个调用者一个。
/// 默认就是放一遍：加载时会把循环强制关掉，不看音频文件自己怎么设的。
/// </summary>
public static class SoundTool
{
    /// <summary>播放器挂在调用者身上的节点名。</summary>
    private const string PlayerName = "SoundPlayer";

    private static readonly Dictionary<string, AudioStream> Cache = new();

    /// <summary>音效没写后缀时按这个顺序试，省得作者还要记住自己导出的是什么格式。</summary>
    private static readonly string[] Extensions = { ".ogg", ".wav", ".mp3" };

    /// <summary>
    /// 找音效文件：先看球自己的 `resource/`，再看 `user://` 根目录；不写后缀就试 `.ogg`/`.wav`/`.mp3`。
    /// 规则和别的资源完全一样，实现在 `BallLibrary.Find`（找法只有一处，改只改那儿）。
    /// </summary>
    public static string Resolve(string sound, string ballId)
    {
        return BallLibrary.Find(sound, ballId, Extensions);
    }

    /// <summary>加载并缓存。找不到文件、后缀不认识、解析失败都返回 null。</summary>
    public static AudioStream Load(string sound, string ballId)
    {
        var path = Resolve(sound, ballId);
        if (path == null)
        {
            GD.PushWarning($"[音效] 找不到音效文件：{sound}（找过球自己的 resource 目录和 user:// 根目录）");
            return null;
        }

        if (Cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        if (!FileAccess.FileExists(path))
        {
            GD.PushWarning($"[音效] 找不到文件：{path}");
            return null;
        }

        var stream = LoadFile(path);
        if (stream == null)
        {
            return null;
        }

        Cache[path] = stream;
        return stream;
    }

    /// <summary>
    /// 在 owner 身上放一遍。没配置音效、文件找不到、owner 不在场景里，都安静地什么都不做。
    /// 同一个 owner 重复触发就重头放（不做叠加）。
    ///
    /// `pitch` 是播放音高（1 = 原样）：**小于 1 更低、更沉闷**，大于 1 更尖。
    /// 同一个音效文件想做出"不一样的手感"（比如同一个撞击声，盾球听起来闷一点），就靠它，
    /// 不用再准备第二份素材。
    /// </summary>
    public static void PlayOnce(Node owner, string sound, string ballId, float pitch = 1f)
    {
        if (owner == null || string.IsNullOrEmpty(sound))
        {
            return;
        }

        var stream = Load(sound, ballId);
        if (stream == null)
        {
            return;
        }

        // 第一次触发时现挂一个播放器，之后复用
        var player = owner.GetNodeOrNull<AudioStreamPlayer>(PlayerName);
        if (player == null)
        {
            player = new AudioStreamPlayer { Name = PlayerName };
            owner.AddChild(player);
        }

        player.Stream = stream;
        player.PitchScale = pitch > 0f ? pitch : 1f; // 防呆：0 或负数会让引擎不播
        player.Play();

        GD.Print($"[音效] 球[{ballId}] 播放 {sound}（{stream.GetLength():0.00} 秒，音高 {player.PitchScale:0.##}）");
    }

    /// <summary>按后缀挑引擎自带的加载器。</summary>
    private static AudioStream LoadFile(string path)
    {
        // 用 System.IO.Path，别 using System.IO：那个命名空间里也有个 FileAccess，会和 Godot.FileAccess 撞名
        var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();

        AudioStream stream = extension switch
        {
            ".wav" => AudioStreamWav.LoadFromFile(path),
            ".ogg" => AudioStreamOggVorbis.LoadFromFile(path),
            ".mp3" => AudioStreamMP3.LoadFromFile(path),
            _ => null,
        };

        if (stream == null)
        {
            GD.PushWarning($"[音效] 读不了这个文件（只认 .wav / .ogg / .mp3）：{path}");
            return null;
        }

        // 默认放一遍：不管文件自己有没有设循环，这里都关掉
        switch (stream)
        {
            case AudioStreamWav wav:
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
                break;
            case AudioStreamOggVorbis ogg:
                ogg.Loop = false;
                break;
            case AudioStreamMP3 mp3:
                mp3.Loop = false;
                break;
        }

        return stream;
    }
}
