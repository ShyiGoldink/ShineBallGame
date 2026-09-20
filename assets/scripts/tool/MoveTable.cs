using Godot;

/// <summary>
/// 招式表：球 Json 里 `attacks` 那一段的读法——**招式名 → 演多久 + 配哪段动画**。
///
/// 读法只有这一处：出招的组件（比如 `2002` 下蛋）来问"这一招演多久"，
/// 外观层（`SpineLook`）来问"这一段动画叫什么"，两边拿的是同一份数据，不会各写各的。
///
/// ```json
/// "attacks": {
///   "egg":   { "time": 5.5, "anim": "attack_egg" },
///   "punch": { "time": 0.3, "anim": "attack_punch" }
/// }
/// ```
///
/// 规矩：
/// * `time` 留 0（或不写）= "用这段动画自己的长度"，由出招的组件决定要不要这么理解；
/// * `anim` 不写 = 没有专属动画，退回按状态找（`attack` → `idle` → `move`）；
/// * 表里没有这一招 = `TryGet` 返回 false，出招的组件退回自己的参数（兼容老写法）。
/// </summary>
public static class MoveTable
{
    /// <summary>这颗球的招式表里有没有这一招。有就把它演多久、配哪段动画给出来。</summary>
    public static bool TryGet(string ballId, string moveId, out float time, out string anim)
    {
        time = 0f;
        anim = string.Empty;

        if (string.IsNullOrEmpty(ballId) || string.IsNullOrEmpty(moveId))
        {
            return false;
        }

        var data = BallData.Load(ballId);
        if (data?.Attacks == null || !data.Attacks.ContainsKey(moveId))
        {
            return false;
        }

        var entry = data.Attacks[moveId].AsGodotDictionary();
        time = JsonTool.GetValue(entry, "time", 0f);
        anim = JsonTool.GetValue(entry, "anim", string.Empty);
        return true;
    }
}
