using Godot;

/// <summary>
/// "我现在站在谁的领域里""我自己有没有领域"这段判定的**唯一出处**。
///
/// 为什么要单独一个文件：领域效果不止一种了——`8002` 是定身、`8004` 是必中斩，
/// 以后还可能有别的。它们的**判定条件完全一样**（站在敌方生效中的领域里？自己有没有领域顶着？），
/// 只是"判定成立之后干什么"不同。判定写一份，效果各写各的。
///
/// 三件事：
/// * `CoveringField(球)` —— 罩住这颗球的**敌方**领域（没有就 null）；
/// * `OwnDomain(球)` —— 这颗球身上**正在生效**的领域组件（没有就 null）；
/// * `OwnerOf(领域)` —— 这个领域是谁展开的（按阵营找那颗球，给"记在谁头上"用）。
/// </summary>
public static class DomainQuery
{
    /// <summary>罩住这颗球的敌方领域（按圆心距离判定，取第一个命中的）。没有就返回 null。</summary>
    public static DomainField CoveringField(Ball ball)
    {
        if (ball == null || ball.GetTree() == null)
        {
            return null;
        }

        foreach (var node in ball.GetTree().GetNodesInGroup(DomainField.FieldsGroup))
        {
            if (node is DomainField field && field.Active
                && field.OwnerGroup != ball.Group && field.Contains(ball.GlobalPosition))
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>这颗球身上正在生效的领域组件（自己展开的）。没开、或者没挂就返回 null。</summary>
    public static Domain OwnDomain(Ball ball)
    {
        if (ball == null)
        {
            return null;
        }

        foreach (var child in ball.GetChildren())
        {
            if (child is Domain domain && domain.Active)
            {
                return domain;
            }
        }

        return null;
    }

    /// <summary>
    /// 这颗球这会儿**有没有在用领域顶着**：已经展开（`Active`）或者正在开（`Pending`，前摇里）都算。
    ///
    /// 为什么前摇也要算：两个领域同时开是对撞，谁都有零点几秒的前摇——
    /// 先展开的那一方要是在这个窗口里把对方锁死，"同时开互相抵消"就变成拼手速了。
    /// 所以"跟对手同时开"能把必中挡下来；**没开领域、也没在开**才真的吃效果。
    /// </summary>
    public static bool Contesting(Ball ball)
    {
        if (ball == null)
        {
            return false;
        }

        foreach (var child in ball.GetChildren())
        {
            if (child is Domain domain && (domain.Active || domain.Pending))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>场上还有没有**别的阵营**的生效中领域（"对手开领域了没有"）。</summary>
    public static bool EnemyDomainUp(Ball ball)
    {
        if (ball == null || ball.GetTree() == null)
        {
            return false;
        }

        foreach (var node in ball.GetTree().GetNodesInGroup(DomainField.FieldsGroup))
        {
            if (node is DomainField field && field.Active && field.OwnerGroup != ball.Group)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>这个领域是谁展开的（按阵营找那颗球）。找不到就返回 null。</summary>
    public static Ball OwnerOf(DomainField field)
    {
        if (field == null || field.GetTree() == null)
        {
            return null;
        }

        foreach (var node in field.GetTree().GetNodesInGroup(Ball.BallsGroup))
        {
            if (node is Ball ball && ball.Group == field.OwnerGroup)
            {
                return ball;
            }
        }

        return null;
    }
}
