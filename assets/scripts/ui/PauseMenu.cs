using Godot;

/// <summary>
/// 对局里的暂停面板：对局中按 Esc 呼出（由 `Game` 打开），上面有"继续 / 返回主菜单 / 退出游戏"。
///
/// 它**必须能在暂停时继续工作**（`ProcessMode = Always`），否则按 Esc 就关不掉自己、
/// 按钮也点不动。注意别把这个设到场景根节点上——那等于暂停失效，球会继续跑
/// （见 `FunctionGuide` 的「约定与坑」第 4 条）。
/// </summary>
public partial class PauseMenu : Control
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always; // 暂停了也要能收 Esc、能点按钮
        Visible = false;
    }

    /// <summary>暂停：先亮出面板，再把整棵树冻住（球、面板、倒计时全停）。</summary>
    public void Pause()
    {
        Visible = true;
        GetTree().Paused = true;
        GD.Print("[对局] 暂停");
    }

    /// <summary>继续：先解冻，再把自己藏起来。</summary>
    public void Resume()
    {
        GetTree().Paused = false;
        Visible = false;
        GD.Print("[对局] 继续");
    }

    /// <summary>按钮：继续。</summary>
    public void OnResumePressed() => Resume();

    /// <summary>按钮：回主菜单（`ReturnToMenu` 自己会先解暂停再切场景）。</summary>
    public void OnMenuPressed() => GameManager.Instance?.ReturnToMenu();

    /// <summary>按钮：退出游戏。</summary>
    public void OnQuitPressed()
    {
        GD.Print("[对局] 退出游戏");
        GetTree().Quit();
    }

    /// <summary>
    /// 暂停中按 Esc = 继续。（对局中按 Esc **开**暂停是 `Game` 的事——那会儿树还没冻，
    /// 它还能收输入；冻住之后再按 Esc 就只有这个面板收得到了。）
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey key || !key.Pressed || key.Echo || key.Keycode != Key.Escape)
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        Resume();
    }
}
