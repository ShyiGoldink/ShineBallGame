using Godot;

/// <summary>
/// 数据面板：战场上左右各一个，显示对应小球的名字、血量、速度、方向、状态。
///
/// 数据是"拉"来的，不是事件推来的：定时直接读球的当前值。
/// 事件是变化流，漏一个就会永久显示错的；而"现在是什么"随时问得出来，
/// 所以面板从结构上就不可能过期。
/// </summary>
public partial class BallDataPanel : PanelContainer
{
    /// <summary>刷新间隔。面板是给人看的，10Hz 足够，也省掉没必要的文本重绘。</summary>
    private const float RefreshInterval = 0.1f;

    private Ball _ball;
    private string _title = string.Empty;
    private Label _titleLabel;
    private Label _hp;
    private Label _speed;
    private Label _direction;
    private Label _state;
    private float _timer;

    public override void _Ready()
    {
        EnsureNodes();
        Refresh();
    }

    /// <summary>绑定要显示的小球。</summary>
    public void Bind(Ball ball, string title)
    {
        _ball = ball;
        _title = title;
        EnsureNodes();
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (_ball == null)
        {
            return;
        }

        _timer += (float)delta;
        if (_timer < RefreshInterval)
        {
            return;
        }

        _timer = 0f;
        Refresh();
    }

    private void EnsureNodes()
    {
        _titleLabel ??= GetNodeOrNull<Label>("Box/Title");
        _hp ??= GetNodeOrNull<Label>("Box/Hp");
        _speed ??= GetNodeOrNull<Label>("Box/Speed");
        _direction ??= GetNodeOrNull<Label>("Box/Direction");
        _state ??= GetNodeOrNull<Label>("Box/State");
    }

    private void Refresh()
    {
        if (_ball == null)
        {
            return;
        }

        Set(_titleLabel, $"{_title} · {_ball.DisplayName}");
        Set(_hp, $"血量 {Mathf.RoundToInt(_ball.Hp)} / {Mathf.RoundToInt(_ball.MaxHp)}");
        Set(_speed, $"速度 {Mathf.RoundToInt(_ball.Velocity.Length())}");

        // 0° 向右，正角度是顺时针（2D 里 Y 轴朝下）
        int degrees = _ball.Velocity.LengthSquared() > 0.01f
            ? Mathf.RoundToInt(Mathf.RadToDeg(_ball.Velocity.Angle()))
            : 0;
        Set(_direction, $"方向 {degrees}°");
        Set(_state, $"状态 {StateText(_ball.State)}");
    }

    /// <summary>只在文字真的变了才写，省掉没必要的重绘。</summary>
    private static void Set(Label label, string text)
    {
        if (label != null && label.Text != text)
        {
            label.Text = text;
        }
    }

    private static string StateText(BallState state) => state switch
    {
        BallState.Spawn => "登场",
        BallState.Move => "移动",
        BallState.Controlled => "受控",
        BallState.Attack => "攻击",
        BallState.Dead => "死亡",
        _ => state.ToString(),
    };
}
