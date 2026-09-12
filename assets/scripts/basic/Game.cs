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

	private readonly List<Ball> _balls = new();
	private Label _countdown;
	private float _timeLeft = CountdownSeconds;
	private bool _counting;

	public override void _Ready()
	{
		_countdown = GetNodeOrNull<Label>("Countdown");

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
		_balls.Add(ball1);
		_balls.Add(ball2);

		// 战前互挂：各自把"给对方"的组件挂到对面身上
		BallAssembler.AttachComponents(ball2, data1.EnemyComponents);
		BallAssembler.AttachComponents(ball1, data2.EnemyComponents);

		// 球这会儿还在登场状态，动不了，等倒计时结束再放出去
		_counting = true;
		ShowCountdown(CountdownSeconds);
	}

	public override void _Process(double delta)
	{
		if (!_counting)
		{
			return;
		}

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

		foreach (var ball in _balls)
		{
			ball.FinishSpawn();
		}

		GD.Print("[对战] 开始！");
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
