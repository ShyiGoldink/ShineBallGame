using Godot;
using System.Collections.Generic;

/// <summary>
/// 战斗场景管理器：从 GameManager 拿到双方选的球 → 装配 → 摆到出生点
/// → 倒计时 3 秒 → 放球开打。
/// </summary>
public partial class Game : Node
{
	private const float CountdownSeconds = 3f;

	/// <summary>玩家 1 出生点（左边）。</summary>
	private static readonly Vector2 Player1Spawn = new Vector2(700f, 540f);

	/// <summary>玩家 2 出生点（右边）。</summary>
	private static readonly Vector2 Player2Spawn = new Vector2(1220f, 540f);

	private Label _countdown;
	private Label _result;
	private float _timeLeft = CountdownSeconds;
	private bool _counting;
	private bool _settled;

	public override void _Ready()
	{
		_countdown = GetNodeOrNull<Label>("Countdown");
		_result = GetNodeOrNull<Label>("Result");

		var manager = GameManager.Instance;
		if (manager == null || !manager.CanStart)
		{
			GD.PushError("[对战] 没拿到双方选的球。战斗场景要从选球界面进，直接跑这个场景是不行的。");
			return;
		}

		var data1 = BallData.Load(manager.Player1Ball);
		var data2 = BallData.Load(manager.Player2Ball);
		if (data1 == null || data2 == null)
		{
			return;
		}

		var ball1 = BallAssembler.Build(data1, Player1Spawn, 1);
		var ball2 = BallAssembler.Build(data2, Player2Spawn, 2);
		if (ball1 == null || ball2 == null)
		{
			return;
		}

		AddChild(ball1);
		AddChild(ball2);

		// 战前互挂：各自把"给对方"的组件挂到对面身上
		BallAssembler.AttachComponents(ball2, data1.EnemyComponents);
		BallAssembler.AttachComponents(ball1, data2.EnemyComponents);

		// 数据面板自己去读球的当前值，不用事件推
		GetNodeOrNull<BallDataPanel>("LeftPanel")?.Bind(ball1, "玩家 1");
		GetNodeOrNull<BallDataPanel>("RightPanel")?.Bind(ball2, "玩家 2");

		// 球这会儿还在登场状态，动不了，等倒计时结束再放出去
		_counting = true;
		ShowCountdown(CountdownSeconds);
	}

	public override void _Process(double delta)
	{
		if (_counting)
		{
			UpdateCountdown(delta);
			return;
		}

		if (!_settled)
		{
			CheckSettled();
		}
	}

	private void UpdateCountdown(double delta)
	{
		_timeLeft -= (float)delta;
		if (_timeLeft > 0f)
		{
			ShowCountdown(_timeLeft);
			return;
		}

		_counting = false;

		if (_countdown != null)
		{
			_countdown.Visible = false;
		}

		// 放球：不查自己维护的名单，直接问场上现在有哪些球，分身出来的也一起放
		foreach (var node in GetTree().GetNodesInGroup(Ball.BallsGroup))
		{
			if (node is Ball ball)
			{
				ball.FinishSpawn();
			}
		}

		GD.Print("[对战] 开始！");
	}

	/// <summary>
	/// 结算判定：场上只剩一个阵营活着就算结束。
	///
	/// 这里用"拉取当前状态"，而不是"等死亡事件"：分身、繁殖这类技能会随时往场上加球，
	/// 拉取天然看得见它们，也不会漏掉任何一次死亡——事件漏一次，结算就永远卡住。
	/// </summary>
	private void CheckSettled()
	{
		var aliveGroups = new HashSet<int>();

		foreach (var node in GetTree().GetNodesInGroup(Ball.BallsGroup))
		{
			if (node is Ball ball && ball.State != BallState.Dead)
			{
				aliveGroups.Add(ball.Group);
			}
		}

		if (aliveGroups.Count > 1)
		{
			return; // 还有两个以上阵营在打
		}

		int winner = 0;
		foreach (var group in aliveGroups)
		{
			winner = group; // 只剩一个阵营时，它就是赢家
		}

        _settled = true;
        ShowResult(winner);

        // 蛋跟着这一局一起清掉：它们的计时跑在 _PhysicsProcess 里，暂停之后就不会再走，
        // 留着的话会一直挂在结算画面上
        ClearEggs();

        // 结算后暂停：球和面板都停住，只有结算提示自己设了 Always，还能闪、还能收输入
        GetTree().Paused = true;
		GD.Print($"[对战] 结算后暂停：{GetTree().Paused}，按任意处退出");
		GetNodeOrNull<ResultPrompt>("Prompt")?.ShowPrompt();
	}

    /// <summary>把场上剩下的蛋清掉（蛋不是球、不在 balls 组里，所以单独清）。</summary>
    private void ClearEggs()
    {
        int count = 0;

        foreach (var node in GetTree().GetNodesInGroup(BallAssembler.EggsGroup))
        {
            node.QueueFree();
            count++;
        }

        if (count > 0)
        {
            GD.Print($"[对战] 清掉场上剩下的 {count} 颗蛋");
        }
    }

    private void ShowResult(int winnerGroup)
	{
		GD.Print(winnerGroup > 0 ? $"[对战] 玩家 {winnerGroup} 获胜" : "[对战] 双方全灭，平局");

		if (_result == null)
		{
			return;
		}

		_result.Visible = true;
		_result.Text = winnerGroup > 0 ? $"玩家 {winnerGroup} 获胜！" : "平局";
	}

	private void ShowCountdown(float timeLeft)
	{
		if (_countdown == null)
		{
			return;
		}

		_countdown.Visible = true;
		_countdown.Text = Mathf.CeilToInt(timeLeft).ToString();
	}
}
