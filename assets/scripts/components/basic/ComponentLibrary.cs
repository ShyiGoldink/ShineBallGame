using Godot;

/// <summary>
/// 按编号创建组件。Json 里 selfcomponents / enemycomponents 的键就是这个编号，
/// 加新组件时在这里补一行。
///
/// 编号段：1xxx 移动 / 2xxx 攻击 / 3xxx 防御 / 4xxx 行为 / 5xxx 形态 / 6xxx 控制 /
/// 7xxx 咒力（资源：池子这种"给别的组件当底子"的东西）/ 8xxx 领域（领域本体和它的效果）。
/// </summary>
public static class ComponentLibrary
{
    public static BallComponent Create(int id)
    {
        switch (id)
        {
            case 1001:
                return new NormalMove();
            case 1002:
                return new JujutsuMotion();
            case 2001:
                return new CollisionAttack();
            case 2002:
                return new EggAttack();
            case 2004:
                return new BasicAttack();
            case 2005:
                return new JujutsuSkill();
            case 2006:
                return new HollowPurple();
            case 2007:
                return new Dismantle();
            case 2008:
                return new Cleave();
            case 2009:
                return new Fuga();
            case 3001:
                return new ConditionalImmune();
            case 3002:
                return new Shield();
            case 3003:
                return new ShieldGauge();
            case 3004:
                return new Limitless();
            case 4001:
                return new NormalDamage();
            case 4002:
                return new Poison();
            case 4003:
                return new StateSound();
            case 4004:
                return new PoisonDamage();
            case 4005:
                return new ReverseTechnique();
            case 4006:
                return new SandbagDamage();
            case 4007:
                return new HpRefill();
            case 4008:
                return new TrueDamage();
            case 5001:
                return new CircleShape();
            case 6001:
                return new Slow();
            case 6002:
                return new MuteAttack();
            case 7001:
                return new CursedEnergy();
            case 7002:
                return new CursedEnergyGauge();
            case 7003:
                return new SlashMark();
            case 8001:
                return new UnlimitedVoid();
            case 8002:
                return new DomainControl();
            case 8003:
                return new MalevolentShrine();
            case 8004:
                return new MalevolentSlash();
            default:
                GD.PushError($"[装配] 没有编号为 {id} 的组件。");
                return null;
        }
    }
}
