using Godot;

/// <summary>
/// 选球界面上的一个球按钮：头像 + 名字。
/// 单独做成预制体（BallButton.tscn），要改样式改那个预制体就行。
/// </summary>
public partial class BallButton : Button
{
    /// <summary>这个按钮代表哪个球（文件夹名）。</summary>
    public string BallId { get; private set; }

    private TextureRect _avatar;
    private Label _name;

    /// <summary>把球的数据填进按钮。</summary>
    public void Setup(BallEntry entry)
    {
        BallId = entry.Id;

        _avatar ??= GetNodeOrNull<TextureRect>("Avatar");
        _name ??= GetNodeOrNull<Label>("NameLabel");

        if (_avatar != null)
        {
            _avatar.Texture = entry.Avatar;
        }

        if (_name != null)
        {
            _name.Text = entry.Name;
        }

        TooltipText = entry.Name;
    }
}
