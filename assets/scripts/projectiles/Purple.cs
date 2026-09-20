using Godot;

/// <summary>
/// 虚式「茈」的那颗球（`2006` 放出来的）：**紫色巨球，出来先蓄势，蓄完重新索敌再冲出去**。
///
/// 飞行、命中判定、留场计时、伤害怎么走，全在基类 `DamageProjectile` 里——
/// 这里只管它自己的三件事：紫色、蓄势那一下、"蓄势完发现没人可打就消失"。
/// </summary>
public partial class Purple : DamageProjectile
{
    /// <summary>紫色。</summary>
    private static readonly Color PurpleColor = new Color(0.72f, 0.35f, 1f, 0.92f);

    /// <summary>
    /// 放出来之前调（进树之前）：伤害、速度、蓄势、留场、大小由 `2006` 的参数决定，
    /// 阵营和 id 跟着放它的那颗球。
    /// </summary>
    public void Launch(Ball owner, string ownerId, float damage, float speed, float delay, float life, float size)
    {
        Setup(owner, owner == null ? 0 : owner.Group, ownerId, damage, "attack.hollow_purple",
            Vector2.Right, speed, delay, life, size, PurpleColor);
    }

    /// <summary>蓄势完了：重新看一眼谁离得近、朝它冲；场上没人就自己收掉。</summary>
    protected override void OnChargeFinished()
    {
        if (!AimAtNearestEnemy())
        {
            GD.Print($"[{SourceType}] {Name} 蓄势完毕，但没有可攻击的敌人，消失");
            QueueFree();
            return;
        }

        GD.Print($"[{SourceType}] {Name} 蓄势完毕，重新索敌后出发");
    }
}
