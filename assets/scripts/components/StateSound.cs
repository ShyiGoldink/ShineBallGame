using Godot;
using System;

/// <summary>
/// 进状态播音效：球**进入指定状态**时播一次音效（默认"攻击"）。
/// `pulipuli` 用它做到"一进攻击状态就出声"。
///
/// 找文件的方式见 `SoundTool`：先在球自己的 `resource/` 里找，再找 `user://` 根目录；
/// 名字**不写后缀**时会依次试 `.ogg` / `.wav` / `.mp3`，所以文件是什么格式都行。
/// </summary>
public partial class StateSound : BallComponent
{
    public override int Id => 4003;

    public override string Type => "behavior.state_sound";

    public override string Description => "球进入指定状态时播一次音效";

    /// <summary>哪个状态下播：写状态名（不区分大小写），默认 attack（攻击）。</summary>
    public string State = "attack";

    /// <summary>音效文件名或路径。可以不写后缀（会自动试 .ogg / .wav / .mp3）。</summary>
    public string Sound = string.Empty;

    private Ball _ball;
    private BallState _triggerState = BallState.Attack;
    private bool _ready;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        State = JsonTool.GetValue(parameters, "state", State);
        Sound = JsonTool.GetValue(parameters, "sound", Sound);
    }

    /// <summary>装配时接线：解析"哪个状态响"，然后订状态变化。</summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，音效不会响。");
            return;
        }

        if (!Enum.TryParse(State, true, out _triggerState))
        {
            GD.PushWarning($"[{Type}] 不认识的状态名 '{State}'，按默认的攻击状态处理。");
            _triggerState = BallState.Attack;
        }

        _ball.Events.Register(EventName.state_changed, new EventResponseFunction { priority = 0, action = OnStateChanged });
        _ready = true;
    }

    private bool OnStateChanged(object arg)
    {
        if (_ready && arg is BallState state && state == _triggerState && !string.IsNullOrEmpty(Sound))
        {
            SoundTool.PlayOnce(this, Sound, _ball.Id);
        }

        return true; // 通知类事件：一律放行，别挡住别的组件收听
    }
}
