using Godot;

/// <summary>
/// 按编号创建组件。Json 里 selfcomponents / enemycomponents 的键就是这个编号，
/// 加新组件时在这里补一行。
/// </summary>
public static class ComponentLibrary
{
    public static BallComponent Create(int id)
    {
        switch (id)
        {
            case 1001:
                return new NormalMove();
            case 2001:
                return new CollisionAttack();
            case 2002:
                return new EggAttack();
            case 2003:
                return new Shift();
            case 3001:
                return new ConditionalImmune();
            case 4001:
                return new NormalDamage();
            case 4002:
                return new Poison();
            case 5001:
                return new CircleShape();
            case 5002:
                return new SpineLook();
            case 6001:
                return new Slow();
            default:
                GD.PushError($"[装配] 没有编号为 {id} 的组件。");
                return null;
        }
    }
}
