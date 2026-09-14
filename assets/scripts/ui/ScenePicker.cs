using Godot;
using System.Collections.Generic;

/// <summary>
/// 场地选择：把 `user://scenes` 里扫到的场地铺成一排按钮（头像 + 名字），点一下换当前场地。
///
/// 和选球不一样，**场地是两个人的共同环境**，所以不做"各自确认"——点中哪个就是哪个，
/// 默认用第一个（`SceneLibrary` 按 id 排序，保证每次一样）。
///
/// 按钮直接复用 `BallButton.tscn`，界面在 `_Ready` 里搭出来，省得再维护一个场景文件。
/// </summary>
public partial class ScenePicker : PanelContainer
{
    private const string ButtonScene = "res://assets/scene/BallButton.tscn";

    /// <summary>当前选中的场地 id。</summary>
    public string SelectedId { get; private set; } = string.Empty;

    private Label _status;
    private readonly ButtonGroup _group = new();

    /// <summary>场地 id → 按钮。默认选中的那个也要按下去，不然界面上看起来一个都没选。</summary>
    private readonly Dictionary<string, BallButton> _buttons = new();

    public override void _Ready()
    {
        var box = new VBoxContainer { Name = "Box" };
        AddChild(box);
        box.AddChild(new Label { Name = "Title", Text = "场地" });

        var row = new HBoxContainer { Name = "Row" };
        box.AddChild(row);

        _status = new Label { Name = "Status" };
        box.AddChild(_status);

        var buttonScene = GD.Load<PackedScene>(ButtonScene);
        if (buttonScene == null)
        {
            GD.PushError($"[场地] 找不到按钮预制体：{ButtonScene}");
            return;
        }

        _group.AllowUnpress = false; // 一直有个场地是被选中的

        var scenes = SceneLibrary.LoadAll();
        scenes.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

        foreach (var entry in scenes)
        {
            var button = buttonScene.Instantiate<BallButton>();
            button.ToggleMode = true;
            button.ButtonGroup = _group;
            button.Setup(entry.Id, entry.Name, entry.Avatar);
            button.Pressed += () => Select(entry.Id);
            _buttons[entry.Id] = button;
            row.AddChild(button);
        }

        if (scenes.Count == 0)
        {
            GD.PushError($"[场地] 一个场地都没扫到，看看 {UserData.Global(UserData.Scenes)}");
            return;
        }

        Select(scenes[0].Id); // 默认用第一个
    }

    /// <summary>换当前场地。</summary>
    public void Select(string sceneId)
    {
        SelectedId = sceneId;

        // 按下状态跟着选中走：程序里选的（默认第一个）和手点出来的看起来要一样
        if (_buttons.TryGetValue(sceneId, out var button))
        {
            button.ButtonPressed = true;
        }

        if (_status != null)
        {
            _status.Text = $"当前场地：{sceneId}";
        }

        GD.Print($"[场地] 选中 {sceneId}");
    }
}
