using Godot;
using System.Collections.Generic;

/// <summary>
/// Spine 外观（`type = 2` 的球用它显示）。
///
/// 在球的 `resource/` 目录里找 `.atlas` + 骨架文件（`.json` / `.skel`），拼成骨骼数据挂到球上，
/// 然后跟着球的五个状态切动画——跟 `1001` 管移动、`6001` 管受控是一个套路：状态变了，动画跟着变。
///
/// 动画名就用状态名：Spawn / Move / Controlled / Attack / Dead → spawn / move / controlled / attack / dead。
/// 找不到就顺兜底链往下找，最后兜到 `idle` / `move`（所以素材只有 idle + attack 也能跑）。
/// 循环：登场 / 移动 / 受控是循环的；攻击、死亡只播一遍（死亡停在最后一帧）。
///
/// C# 里没有 Spine 的类（它是 GDExtension 带进来的，只有引擎和 GDScript 认识），
/// 所以这里全部走 `ClassDB` + `Call/Set/Get`，不写类型。
/// </summary>
public partial class SpineLook : BallComponent
{
    public override int Id => 5002;

    public override string Type => "look.spine";

    public override string Description => "type=2 的球用它显示：按球的五个状态切 Spine 动画";

    /// <summary>骨架整体缩放。想让角色在场上占 150px，就配 150 ÷ 角色在 Spine 里的高度。</summary>
    public float Scale = 1f;

    /// <summary>五个状态各自找动画的顺序，前面找不到就试后面。</summary>
    private static readonly string[][] Clips =
    {
        new[] { "spawn", "idle", "move" },      // Spawn 登场
        new[] { "move", "idle" },               // Move 移动
        new[] { "controlled", "idle", "move" }, // Controlled 受控
        new[] { "attack", "idle", "move" },     // Attack 攻击
        new[] { "dead", "idle", "move" },       // Dead 死亡
    };

    /// <summary>按球 id 缓存骨骼数据：同一种球的多个实例共用一份图集。</summary>
    private static readonly Dictionary<string, GodotObject> Cache = new();

    private Ball _ball;
    private GodotObject _sprite;
    private GodotObject _data;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Scale = JsonTool.GetValue(parameters, "scale", Scale);
    }

    /// <summary>
    /// 装配时调：把资源读进来、看齐不齐，**不碰场景**。
    /// 返回 false 表示这颗球用不了 Spine（缺文件之类），调用方应该回落到贴图。
    /// </summary>
    public bool Prepare(Ball ball)
    {
        _ball = ball;
        _data = LoadData(ball.Id);
        if (_data == null)
        {
            return false;
        }

        // 装配阶段球还没进场景树，这时候挂子节点不会被引擎拦下来
        // （放到 _Ready 里做的话，球的 _Ready 正在建子节点，add_child 会失败）
        return BuildSprite();
    }

    public override void _Ready()
    {
        if (_sprite == null || _ball == null)
        {
            return;
        }

        _ball.Events.Register(EventName.state_changed, new EventResponseFunction { priority = 0, action = OnStateChanged });

        // 按当前状态先起一个（球这会儿一般是登场状态）
        Play(_ball.State);
    }

    private bool OnStateChanged(object arg)
    {
        if (arg is BallState state)
        {
            Play(state);
        }

        return true;
    }

    /// <summary>建 SpineSprite 挂到球下面，并把贴图藏掉。</summary>
    private bool BuildSprite()
    {
        var sprite = ClassDB.Instantiate("SpineSprite").AsGodotObject();
        if (sprite == null)
        {
            GD.PushError($"[{Type}] 引擎里没有 SpineSprite，扩展是不是没加载？");
            return false;
        }

        sprite.Set("name", "Spine");
        sprite.Set("scale", Vector2.One * Scale);
        sprite.Set("skeleton_data_res", _data);

        // 挂在球下面（得挂在球的节点下，不然位置是相对画布的，不跟着球走）
        _ball.Call("add_child", sprite);

        var body = _ball.GetNodeOrNull<Sprite2D>("Body");
        if (body != null)
        {
            body.Visible = false; // 用了 Spine 就不显示那张贴图
        }

        _sprite = sprite;
        return true;
    }

    /// <summary>按状态切动画：先把轨道清掉再放，免得排成一队。</summary>
    private void Play(BallState state)
    {
        if (_sprite == null)
        {
            return;
        }

        var animationState = _sprite.Call("get_animation_state").AsGodotObject();
        if (animationState == null)
        {
            return; // 还没准备好，等下一次状态变化
        }

        var clip = Pick(state);
        if (clip == null)
        {
            return; // 一个都没找到，就保持现在的姿势
        }

        animationState.Call("clear_track", 0);
        animationState.Call("add_animation", clip, 0f, IsLooping(state), 0);

        GD.Print($"[{Type}] {_ball?.Name} 播 {clip}（{state}）");
    }

    /// <summary>挑这个状态该播哪个动画：先按名字找，找不到顺兜底链。</summary>
    private string Pick(BallState state)
    {
        int index = (int)state;
        if (_data == null || index < 0 || index >= Clips.Length)
        {
            return null;
        }

        foreach (var candidate in Clips[index])
        {
            // 注意：找不到动画时返回的是一个"空对象"，VariantType 是 Object 不是 Nil，
            // 必须看 AsGodotObject() 是不是 null
            if (_data.Call("find_animation", candidate).AsGodotObject() != null)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>登场 / 移动 / 受控循环；攻击、死亡只播一遍（死亡停在最后一帧）。</summary>
    private static bool IsLooping(BallState state) =>
        state is BallState.Spawn or BallState.Move or BallState.Controlled;

    /// <summary>在球的 resource 目录里找 .atlas + 骨架文件，拼成骨骼数据（按球 id 缓存）。</summary>
    private GodotObject LoadData(string ballId)
    {
        if (string.IsNullOrEmpty(ballId))
        {
            return null;
        }

        if (Cache.TryGetValue(ballId, out var cached))
        {
            return cached;
        }

        var folder = UserData.Balls + ballId + "/resource/";
        if (!DirAccess.DirExistsAbsolute(folder))
        {
            GD.PushWarning($"[{Type}] 找不到球的目录：{folder}");
            return null;
        }

        string atlasPath = null;
        string skeletonPath = null;

        foreach (var file in DirAccess.GetFilesAt(folder))
        {
            var lower = file.ToLowerInvariant();

            if (atlasPath == null && lower.EndsWith(".atlas"))
            {
                atlasPath = folder + file;
            }
            else if (skeletonPath == null && (lower.EndsWith(".json") || lower.EndsWith(".spine-json")
                     || lower.EndsWith(".skel") || lower.EndsWith(".spskel")))
            {
                skeletonPath = folder + file;
            }
        }

        if (atlasPath == null || skeletonPath == null)
        {
            GD.PushWarning($"[{Type}] {ballId} 里没凑齐 Spine 资源（要 .atlas + .json/.skel），回落到贴图。");
            return null;
        }

        var atlas = ClassDB.Instantiate("SpineAtlasResource").AsGodotObject();
        var skeletonFile = ClassDB.Instantiate("SpineSkeletonFileResource").AsGodotObject();
        var data = ClassDB.Instantiate("SpineSkeletonDataResource").AsGodotObject();

        // 扩展没加载时 ClassDB 会给出 null；这时候不能直接往下调，得干净地回落贴图
        if (atlas == null || skeletonFile == null || data == null)
        {
            GD.PushWarning($"[{Type}] 引擎里没有 Spine 的类（扩展没加载？），回落到贴图。");
            return null;
        }

        atlas.Call("load_from_atlas_file", atlasPath);
        skeletonFile.Call("load_from_file", skeletonPath);

        data.Set("atlas_res", atlas);
        data.Set("skeleton_file_res", skeletonFile);

        Cache[ballId] = data;
        GD.Print($"[{Type}] {ballId} 载入 Spine：{System.IO.Path.GetFileName(atlasPath)} + {System.IO.Path.GetFileName(skeletonPath)}");
        return data;
    }
}
