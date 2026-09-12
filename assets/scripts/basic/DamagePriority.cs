public static class DamagePriority
{
    // 数值越大越优先，以绝对免疫作为最高级标准
    public const int AbsoluteImmune = 1000;   // 绝对免疫（无敌）
    public const int Silence        = 900;  // 沉默：直接扣血，跳过后续防御
    public const int TrueDamage     = 800;  // 真伤：无视护盾等
    public const int Shield         = 700;  // 护盾（非阻塞）
    public const int DamageOverTime = 600;  // 持续扣血（阻塞）
    public const int NormalDamage   = 500;  // 一般扣血
}
