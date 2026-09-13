using Godot;

/// <summary>
/// 结算提示：心跳一样一闪一闪的"按任意处退出"，按一下就回主界面。
///
/// 它必须能在暂停状态下继续工作，所以把自己的 ProcessMode 设成 Always。
/// 注意别把这个设到父节点上——ProcessMode 会被子节点继承，
/// 挂在根节点上等于暂停失效（球会继续跑）。
/// </summary>
public partial class ResultPrompt : Label
{
    /// <summary>闪一次的周期（秒）。</summary>
    private const float PulsePeriod = 1.2f;

    private float _time;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always; // 暂停了也要闪、也要收输入
        Visible = false;
    }

    /// <summary>结算时由战斗场景调用。</summary>
    public void ShowPrompt()
    {
        _time = 0f;
        Modulate = new Color(1f, 1f, 1f, 1f);
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _time += (float)delta;
        float alpha = 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(_time * Mathf.Tau / PulsePeriod));
        Modulate = new Color(1f, 1f, 1f, alpha);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        // 只认真实按下，忽略长按产生的重复事件，免得结算瞬间被一直按着的键关掉
        bool pressed = @event switch
        {
            InputEventKey key => key.Pressed && !key.Echo,
            InputEventMouseButton mouse => mouse.Pressed,
            InputEventScreenTouch touch => touch.Pressed,
            _ => false,
        };

        if (!pressed)
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        GameManager.Instance?.ReturnToMenu();
    }
}
