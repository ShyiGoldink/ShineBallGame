using System;

/// <summary>
/// 球身上"要给面板看的一行数"。
///
/// 组件在装配时（自己的 `Bind` 里）登记一条，左右面板每 0.1 秒**现问现取**：
/// 面板只认这张表，不认识护盾、也不认识任何具体组件——所以加一种要显示的读数，
/// 面板一行都不用改（跟"加组件不用动装配器"是同一个思路）。
///
/// `Value` 是"现在该显示什么"，每次刷新都会调用一次，所以要**现算**，别把值缓存下来
/// （缓存了就又变成"事件流"，漏一次就永久显示错的）。
/// </summary>
public sealed class BallReadout
{
    /// <summary>这一行叫什么。面板会显示成 "护盾 120 / 400"。</summary>
    public readonly string Label;

    /// <summary>现算这一行的值。</summary>
    public readonly Func<string> Value;

    public BallReadout(string label, Func<string> value)
    {
        Label = label;
        Value = value;
    }
}
