using Godot;

/// <summary>
/// 选球页：左边玩家 1、右边玩家 2，各自挑球并确认，
/// 双方都确认之后中间的"开始游戏"才能点。
/// 挂在 index.tscn 的 Select 节点上。
/// </summary>
public partial class SelectPage : Control
{
    private BallPicker _left;
    private BallPicker _right;
    private ScenePicker _scene;
    private Button _start;

    public override void _Ready()
    {
        _left = GetNodeOrNull<BallPicker>("Row/LeftPicker");
        _right = GetNodeOrNull<BallPicker>("Row/RightPicker");
        _scene = GetNodeOrNull<ScenePicker>("Row/Center/ScenePicker");
        _start = GetNodeOrNull<Button>("Row/Center/StartGame");

        if (_start != null)
        {
            _start.Disabled = true; // 双方都确认之后才放开
            _start.Pressed += OnStartPressed;
        }

        var balls = BallLibrary.LoadAll();
        GD.Print($"[选球] 扫到 {balls.Count} 个球");

        if (balls.Count == 0)
        {
            GD.PushError($"[选球] 一个球都没扫到，看看 {UserData.Global(UserData.Balls)}");
        }

        _left?.Setup("玩家 1", balls);
        _right?.Setup("玩家 2", balls);

        if (_left != null)
        {
            _left.Confirmed += OnPickerConfirmed;
            _left.Unconfirmed += OnPickerUnconfirmed;
        }

        if (_right != null)
        {
            _right.Confirmed += OnPickerConfirmed;
            _right.Unconfirmed += OnPickerUnconfirmed;
        }
    }

    /// <summary>
    /// 有玩家点了"重选"：先把"开始游戏"重新禁掉，等两边都重新确认过再放开。
    /// </summary>
    private void OnPickerUnconfirmed()
    {
        if (_start != null)
        {
            _start.Disabled = true;
        }
    }

    private void OnPickerConfirmed(string ballId)
    {
        if (_start == null || _left == null || _right == null)
        {
            return;
        }

        if (_left.IsConfirmed && _right.IsConfirmed)
        {
            _start.Disabled = false;
            GD.Print($"[选球] 双方已确认：{_left.SelectedId} vs {_right.SelectedId}");
        }
    }

    private void OnStartPressed()
    {
        // 双方选的球 + 选中的场地交给单例，由它带进战斗场景
        GameManager.Instance?.StartGame(_left?.SelectedId, _right?.SelectedId, _scene?.SelectedId);
    }
}
