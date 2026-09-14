using Godot;
using System.Collections.Generic;

/// <summary>
/// 一个玩家的选球面板：网格里摆着所有球，点一下换当前选择，点确认之后锁死。
/// 左右两个玩家各用一个，所以做成预制体（BallPicker.tscn）。
/// </summary>
public partial class BallPicker : PanelContainer
{
    /// <summary>确认时发出，带上选中的球 id。</summary>
    [Signal]
    public delegate void ConfirmedEventHandler(string ballId);

    /// <summary>取消确认（点"重选"）时发出：选球界面收到就重新禁掉"开始游戏"。</summary>
    [Signal]
    public delegate void UnconfirmedEventHandler();

    private const string ButtonScene = "res://assets/scene/BallButton.tscn";

    /// <summary>当前选中的球 id，还没选就是空字符串。</summary>
    public string SelectedId { get; private set; } = string.Empty;

    /// <summary>是否已经确认。</summary>
    public bool IsConfirmed { get; private set; }

    private GridContainer _grid;
    private Label _status;
    private Button _confirm;
    private string _title = string.Empty;
    private readonly ButtonGroup _group = new();

    public override void _Ready()
    {
        EnsureNodes();

        if (_confirm != null)
        {
            _confirm.Pressed += OnConfirmPressed;
        }

        RefreshStatus();
    }

    /// <summary>把扫到的小球铺进网格。</summary>
    public void Setup(string title, List<BallEntry> balls)
    {
        EnsureNodes();

        var titleLabel = GetNodeOrNull<Label>("Box/Title");
        _title = title;
        if (titleLabel != null)
        {
            titleLabel.Text = title;
        }

        if (_grid == null)
        {
            GD.PushError("[选球] 预制体里找不到 Box/Grid。");
            return;
        }

        var buttonScene = GD.Load<PackedScene>(ButtonScene);
        if (buttonScene == null)
        {
            GD.PushError($"[选球] 找不到按钮预制体：{ButtonScene}");
            return;
        }

        _group.AllowUnpress = false; // 选过之后必须一直有个选中的

        foreach (var entry in balls)
        {
            var button = buttonScene.Instantiate<BallButton>();
            button.ToggleMode = true;
            button.ButtonGroup = _group;
            button.Setup(entry);
            button.Pressed += () => OnBallPressed(entry);
            _grid.AddChild(button);
        }

        GD.Print($"[选球] {title} 生成了 {_grid.GetChildCount()} 个按钮");

        RefreshStatus();
    }

    private void EnsureNodes()
    {
        _grid ??= GetNodeOrNull<GridContainer>("Box/Grid");
        _status ??= GetNodeOrNull<Label>("Box/Status");
        _confirm ??= GetNodeOrNull<Button>("Box/Confirm");
    }

    private void OnBallPressed(BallEntry entry)
    {
        if (IsConfirmed)
        {
            return; // 确认之后不能再选
        }

        SelectedId = entry.Id;
        RefreshStatus();
    }

    private void OnConfirmPressed()
    {
        if (IsConfirmed)
        {
            OnReselectPressed();
            return;
        }

        if (string.IsNullOrEmpty(SelectedId))
        {
            GD.Print("[选球] 还没选球，不能确认。");
            return;
        }

        IsConfirmed = true;

        // 确认之后把按钮全禁掉：不然点一下虽然不换选择，
        // 选中框还是会被点着，看起来像还能改。
        if (_grid != null)
        {
            foreach (var child in _grid.GetChildren())
            {
                if (child is Button button)
                {
                    button.Disabled = true;
                }
            }
        }

        // 确认按钮本身不禁：它变成"重选"，想反悔就点它
        SetConfirmButton("重选");

        RefreshStatus();
        EmitSignal(SignalName.Confirmed, SelectedId);
    }

    /// <summary>
    /// 重选：把确认放掉，按钮重新能点，并告诉外面"这个玩家又没定了"。
    /// 选球的选中框不特意清掉——玩家多半是想换一个，留着高亮更好认。
    /// </summary>
    private void OnReselectPressed()
    {
        IsConfirmed = false;

        if (_grid != null)
        {
            foreach (var child in _grid.GetChildren())
            {
                if (child is Button button)
                {
                    button.Disabled = false;
                }
            }
        }

        SetConfirmButton("确认");

        RefreshStatus();
        EmitSignal(SignalName.Unconfirmed);
        GD.Print($"[选球] {_title} 取消确认，可以重新选");
    }

    private void SetConfirmButton(string text)
    {
        if (_confirm != null)
        {
            _confirm.Disabled = false;
            _confirm.Text = text;
        }
    }

    private void RefreshStatus()
    {
        if (_status == null)
        {
            return;
        }

        if (IsConfirmed)
        {
            _status.Text = $"已确认：{SelectedId}";
        }
        else if (string.IsNullOrEmpty(SelectedId))
        {
            _status.Text = "未选择";
        }
        else
        {
            _status.Text = $"当前选择：{SelectedId}";
        }
    }
}
