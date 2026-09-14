using Godot;

/// <summary>
/// 控制权仲裁：一颗球上可能同时挂着好几个"想要方向盘"的组件（减速、眩晕……），
/// 但球只有一个移动，所以必须确定地选出一个来驱动。规则（从粗到细）：
///
/// 1. 只看控制类组件（`ControlCategory` 不为 null 的）；
/// 2. 先比**类别优先级** `ControlCategoryPriority`（大的赢）——眩晕一般排在减速前面；
/// 3. 同类别再比**强度** `ControlStrength`（大的赢）——"减得更狠的那个说了算"；
/// 4. 还一样就用组件在球下的顺序兜底，保证结果唯一、可复现。
///
/// 落到效果上就是"阻塞/非阻塞"那套：**强的那一个拿到了方向盘，弱的驱动自然就不生效**
/// （弱的那条线还活着，只是球不听它的）。
/// </summary>
public static class BallControl
{
    /// <summary>这颗球现在该由哪个控制组件驱动；没有控制类组件就返回 null。</summary>
    public static BallComponent PickDriver(Ball ball)
    {
        BallComponent best = null;
        int bestIndex = int.MaxValue;

        if (ball == null)
        {
            return null;
        }

        var children = ball.GetChildren();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not BallComponent candidate || candidate.ControlCategory == null)
            {
                continue;
            }

            if (best == null || IsBetter(candidate, i, best, bestIndex))
            {
                best = candidate;
                bestIndex = i;
            }
        }

        return best;
    }

    /// <summary>a 是不是比 b 更该拿方向盘。</summary>
    private static bool IsBetter(BallComponent a, int aIndex, BallComponent b, int bIndex)
    {
        if (a.ControlCategoryPriority != b.ControlCategoryPriority)
        {
            return a.ControlCategoryPriority > b.ControlCategoryPriority;
        }

        if (a.ControlStrength != b.ControlStrength)
        {
            return a.ControlStrength > b.ControlStrength;
        }

        // 类别不同、强度又一样时，不硬比（两者不可比），按挂载顺序取先来的那个
        return aIndex < bIndex;
    }
}
