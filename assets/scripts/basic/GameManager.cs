using Godot;

/// <summary>
/// 对局单例：负责把"选好的球"带进战斗场景，并管理这一局的生命周期。
/// 在项目设置里注册成自动加载（autoload），任何场景都能用 GameManager.Instance 拿到它，
/// 所以选球界面的信息不会随着场景切换丢掉。
/// </summary>
public partial class GameManager : Node
{
    private const string GameScene = "res://assets/scene/game.tscn";
    private const string MenuScene = "res://assets/scene/index.tscn";

    /// <summary>全局唯一实例。</summary>
    public static GameManager Instance { get; private set; }

    /// <summary>玩家 1 选中的球 id（文件夹名，比如 NormalBall）。</summary>
    public string Player1Ball { get; private set; } = string.Empty;

    /// <summary>玩家 2 选中的球 id。</summary>
    public string Player2Ball { get; private set; } = string.Empty;

    /// <summary>双方都选好球了没有。战斗场景靠它决定能不能开局。</summary>
    public bool CanStart => !string.IsNullOrEmpty(Player1Ball) && !string.IsNullOrEmpty(Player2Ball);

    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            GD.PushWarning("[对战] GameManager 被加载了两次，多余的那个丢掉。");
            QueueFree();
            return;
        }

        Instance = this;
        GD.Print("[对战] GameManager 就绪");
    }

    /// <summary>双方都确认选球之后调这里：记下选球，然后进战斗场景。</summary>
    public void StartGame(string player1BallId, string player2BallId)
    {
        if (string.IsNullOrEmpty(player1BallId) || string.IsNullOrEmpty(player2BallId))
        {
            GD.PushError("[对战] 有玩家还没选球，不能开始。");
            return;
        }

        Player1Ball = player1BallId;
        Player2Ball = player2BallId;

        GD.Print($"[对战] 开始：{Player1Ball} vs {Player2Ball}");
        GetTree().ChangeSceneToFile(GameScene);
    }

    /// <summary>
    /// 回主界面。先解开暂停再切场景——不然新场景一进去就是冻结的，
    /// 这就是"暂停之后整个游戏出 bug"最常见的那种。
    /// </summary>
    public void ReturnToMenu()
    {
        GetTree().Paused = false;

        GD.Print("[对战] 回到主界面");
        GetTree().ChangeSceneToFile(MenuScene);
    }
}
