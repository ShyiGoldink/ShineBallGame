using Godot;
using System.Collections.Generic;

/// <summary>
/// Spine 那一层：只管"把骨架挂到球上、跟着状态切动画"，别的都不管
/// ——外观的总入口在 `BallLook`，这里只是它的其中一个来源。
///
/// 为什么单独一个文件、而且写得这么薄：C# 里**根本没有 Spine 的类**
/// （GDExtension 只对引擎和 GDScript 可见），所以这里全程是 `ClassDB.Instantiate`
/// 加 `Call/Set` 的字符串调用，**编译期查不出错**。把风险关在一个文件里，
/// 改它的时候跑一次实机就能确认，不会把风险扩散到别处。
///
/// 动画跟五大状态绑定：动画名就用状态名，找不到顺兜底链往下找（见 `Clips`）。
/// 登场 / 移动 / 受控循环；攻击、死亡只播一遍（死亡停在最后一帧）。
/// </summary>
public static class SpineLook
{
    /// <summary>五个状态各自找动画的顺序，前面找不到就试后面的。</summary>
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

    /// <summary>
    /// 把 Spine 挂到球上。返回 false 表示这颗球用不了 Spine（缺资源、扩展没加载之类），
    /// 调用方（`BallLook.Apply`）应该回落到贴图。
    ///
    /// 整套动作都在装配阶段做完——那时球还没进场景树，加子节点不会被引擎拦下来；
    /// 等进了树再 add_child 会报 "Parent node is busy setting up children"。
    /// </summary>
    public static bool Attach(Ball ball, float scale)
    {
        var data = LoadData(ball?.Id);
        if (data == null)
        {
            return false;
        }

        var sprite = ClassDB.Instantiate("SpineSprite").AsGodotObject();
        if (sprite == null)
        {
            GD.PushWarning("[Spine] 引擎里没有 SpineSprite（扩展没加载？），回落到贴图。");
            return false;
        }

        sprite.Set("name", "Spine");
        sprite.Set("scale", Vector2.One * scale);
        sprite.Set("skeleton_data_res", data);
        ball.Call("add_child", sprite);

        var body = ball.GetNodeOrNull<Sprite2D>("Body");
        if (body != null)
        {
            body.Visible = false; // 用了 Spine 就不显示那张贴图
        }

        // 状态一变就切动画。优先级 0：状态事件没有先后之争；返回 true 是为了不挡住别人收听。
        ball.Events.Register(EventName.state_changed, new EventResponseFunction
        {
            priority = 0,
            action = arg =>
            {
                if (arg is BallState state)
                {
                    // 攻击状态不在这儿切：那一招的动画由下面的 attack_started 指定。
                    // 不然进攻击时会先按状态播一下、再被招式动画盖掉，白播一帧。
                    if (state != BallState.Attack)
                    {
                        Play(ball, sprite, data, state);
                    }
                }

                return true;
            },
        });

        // 出招（含换招）：动画名先看招式表里给这一招配的那一段，没有才退回按状态找
        ball.Events.Register(EventName.attack_started, new EventResponseFunction
        {
            priority = 0,
            action = arg =>
            {
                Play(ball, sprite, data, BallState.Attack, arg as string);
                return true;
            },
        });

        Play(ball, sprite, data, ball.State); // 先按当前状态起一个
        return true;
    }

    /// <summary>
    /// 切动画：先把轨道清掉再放，免得排成一队。
    /// `moveId` 是"这一下演的是哪一招"（为空就是普通的状态切换）。
    /// </summary>
    private static void Play(Ball ball, GodotObject sprite, GodotObject data, BallState state, string moveId = null)
    {
        var animationState = sprite.Call("get_animation_state").AsGodotObject();
        if (animationState == null)
        {
            return; // 还没准备好，等下一次状态变化
        }

        var clip = Pick(ball, data, state, moveId);
        if (clip == null)
        {
            return; // 一个都没找到，就保持现在的姿势
        }

        animationState.Call("clear_track", 0);
        animationState.Call("add_animation", clip, 0f, IsLooping(state), 0);

        GD.Print($"[Spine] {ball.Name} 播 {clip}（{state}{(string.IsNullOrEmpty(moveId) ? "" : " / " + moveId)}）");
    }

    /// <summary>
    /// 挑这一段该播哪个动画：**先看招式表里给这一招配的动画名**，
    /// 没配（或那段动画不存在）再按状态顺兜底链往下找。
    /// </summary>
    private static string Pick(Ball ball, GodotObject data, BallState state, string moveId = null)
    {
        if (!string.IsNullOrEmpty(moveId)
            && MoveTable.TryGet(ball.Id, moveId, out _, out var moveAnim)
            && !string.IsNullOrEmpty(moveAnim)
            && HasAnimation(data, moveAnim))
        {
            return moveAnim;
        }

        int index = (int)state;
        if (data == null || index < 0 || index >= Clips.Length)
        {
            return null;
        }

        foreach (var candidate in Clips[index])
        {
            if (HasAnimation(data, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 骨架里有没有这段动画。
    /// 注意：找不到时返回的是"空对象"，VariantType 是 Object 不是 Nil，
    /// 所以必须看 `AsGodotObject()` 是不是 null。
    /// </summary>
    private static bool HasAnimation(GodotObject data, string name) =>
        data != null && data.Call("find_animation", name).AsGodotObject() != null;

    /// <summary>登场 / 移动 / 受控循环；攻击、死亡只播一遍（死亡停在最后一帧）。</summary>
    private static bool IsLooping(BallState state) =>
        state is BallState.Spawn or BallState.Move or BallState.Controlled;

    /// <summary>
    /// 在球的 `resource/` 目录里找 `.atlas` + 骨架文件（`.json` / `.skel`），拼成骨骼数据。
    /// 按球 id 缓存：同一种球只读一次，两个玩家里都选它时共用一份图集。
    /// </summary>
    private static GodotObject LoadData(string ballId)
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
            GD.PushWarning($"[Spine] 找不到球的目录：{folder}");
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
            GD.PushWarning($"[Spine] {ballId} 里没凑齐资源（要 .atlas + .json/.skel），回落到贴图。");
            return null;
        }

        var atlas = ClassDB.Instantiate("SpineAtlasResource").AsGodotObject();
        var skeletonFile = ClassDB.Instantiate("SpineSkeletonFileResource").AsGodotObject();
        var data = ClassDB.Instantiate("SpineSkeletonDataResource").AsGodotObject();

        // 扩展没加载时 ClassDB 会给 null；这时候不能直接往下调，得干净地回落贴图
        if (atlas == null || skeletonFile == null || data == null)
        {
            GD.PushWarning("[Spine] 引擎里没有 Spine 的类（扩展没加载？），回落到贴图。");
            return null;
        }

        atlas.Call("load_from_atlas_file", atlasPath);
        skeletonFile.Call("load_from_file", skeletonPath);
        data.Set("atlas_res", atlas);
        data.Set("skeleton_file_res", skeletonFile);

        Cache[ballId] = data;
        GD.Print($"[Spine] {ballId} 载入 {System.IO.Path.GetFileName(atlasPath)} + {System.IO.Path.GetFileName(skeletonPath)}");
        return data;
    }
}
