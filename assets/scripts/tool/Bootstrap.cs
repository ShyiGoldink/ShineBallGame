using Godot;

/// <summary>
/// 启动引导：游戏一跑起来先把 user:// 下的数据准备好，之后任何场景读数据都不会落空。
/// 它在项目设置里注册成了自动加载（autoload），所以比第一个场景还早执行。
/// </summary>
public partial class Bootstrap : Node
{
    public override void _Ready()
    {
        DataSeeder.SeedMissing();
    }
}
