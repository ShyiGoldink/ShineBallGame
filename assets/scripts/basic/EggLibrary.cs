using Godot;

/// <summary>
/// 蛋的编号表：编号 → 蛋类。加一种蛋就在这里补一行。
///
/// 为什么不并进 `ComponentLibrary`：蛋不是"挂在球身上的组件"，而是**自己独立的东西**
/// （普通节点，不是球）。编号沿用同一套（2xxx 攻击类），所以 `2003` 还是屎蛋。
/// </summary>
public static class EggLibrary
{
    public static Egg Create(int id)
    {
        switch (id)
        {
            case 2003:
                return new ShitEgg();
            default:
                GD.PushError($"[装配] 没有编号为 {id} 的蛋。");
                return null;
        }
    }
}
