using Godot;
using System;
using System.Collections.Generic;

	/// <summary>
	/// 这是基类小球
	/// </summary>
public partial class Ball : CharacterBody2D
{
	/// <summary>
	/// 所有小球进场时都会加进这个节点组。想知道"场上现在有哪些球"，查这个组就行——
	/// 分身、繁殖出来的球也会自动在里面，不用谁去维护名单。
	/// </summary>
	public const string BallsGroup = "balls";

	/// <summary>
	/// 小的事件系统，用于事件传递
	/// </summary>
	public BallEvent Events { get; } = new();

	/// <summary>显示用的名字，装配时从 Json 里填。</summary>
	public string DisplayName { get; set; } = string.Empty;

	/// <summary>球的 id，就是数据文件夹名（比如 NormalBall），装配时从 Json 里填。
	/// 注意别用节点名代替它：两颗一样的球撞名时引擎会给节点改名。</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// 要给面板看的读数。组件在装配时往里登记"标签 + 现算函数"，左右面板定时来拉。
	/// 谁登记谁负责现算，面板不缓存、也不认识任何具体组件——加一种要显示的读数，
	/// 面板一行都不用改。
	/// </summary>
	public List<BallReadout> Readouts { get; } = new();

	/// <summary>只读地看当前状态（面板、调试用）；改状态仍然只能走公开的切换方法。</summary>
	public BallState State => _state;

	private float _hp;
	private float _maxHp = 100f;
	private HealthBar _healthBar;

	public float Hp
	{
    	get { return _hp; }
    	set
		{
			_hp = value;
			RefreshHealthBar();

			// 血扣光了就进死亡状态。NormalMove 只在移动状态里动，所以球会自己停下来。
			if (_hp <= 0f)
			{
				ChangeState(BallState.Dead);
			}
		}
	}

	/// <summary>血量上限。血条按百分比显示颜色，所以上限是必需的。</summary>
	public float MaxHp
	{
		get { return _maxHp; }
		set
		{
			_maxHp = value;
			RefreshHealthBar();
		}
	}

	/// <summary>把当前血量推给身上的血条；身上没有血条就什么都不做。</summary>
	private void RefreshHealthBar()
	{
		if (_healthBar == null)
		{
			foreach (var child in GetChildren())
			{
				if (child is HealthBar bar)
				{
					_healthBar = bar;
					break;
				}
			}
		}

		_healthBar?.SetHealth(_hp, _maxHp);
	}

	private int _group;

	public int Group
	{
		get {return _group;}
		set
		{
			_group = value;
		}
	}

	public override void _Ready()
	{
		AddToGroup(BallsGroup);
	}

	/// <summary>当前状态。私有：外面只能通过公开的切换方法改，或者订阅状态变化事件。</summary>
	private BallState _state = BallState.Spawn;

	/// <summary>受控状态的剩余时间。</summary>
	private float _controlLeft;

	/// <summary>攻击状态的剩余时间。</summary>
	private float _attackLeft;

	/// <summary>
	/// 让球进入受控状态，持续 duration 秒。
	/// 已经在受控中时，repeat 决定这段时间怎么处理：重置、叠加、还是不理会。
	/// 时间走完后自动回到移动状态。
	/// </summary>
	public void BeControlled(float duration, ControlRepeat repeat)
	{
		// 改状态之前先看现在是什么状态
		if (_state == BallState.Dead)
		{
			return;
		}

		if (_state == BallState.Controlled)
		{
			switch (repeat)
			{
				case ControlRepeat.Reset:
					_controlLeft = duration;
					break;
				case ControlRepeat.Add:
					_controlLeft += duration;
					break;
				case ControlRepeat.Ignore:
					break;
			}
			return;
		}

		if (_state != BallState.Move)
		{
			// 攻击等状态下不接受控制，这条规则要改再说
			return;
		}

		_controlLeft = duration;
		ChangeState(BallState.Controlled);
	}

	/// <summary>登场结束：从登场状态切到移动状态，球开始自己跑。</summary>
	public void FinishSpawn()
	{
		if (_state == BallState.Spawn)
		{
			ChangeState(BallState.Move);
		}
	}

	/// <summary>
	/// 让球进入攻击状态，持续 duration 秒，时间到了自动回到移动状态。
	/// 攻击状态是给"有动作的攻击"用的：移动类组件在攻击状态里不驱动球（球会停住），
	/// 动画、前摇、产蛋这些挂在这个状态的开始时刻上。
	///
	/// 只有移动中（或者本来就在攻击中，用来刷新时间）能进；
	/// 登场、受控、死亡都不接受——攻击要不要在这些状态下强行插队，等玩法定了再说。
	/// </summary>
	public void BeginAttack(float duration)
	{
		if (_state == BallState.Dead)
		{
			return;
		}

		if (_state != BallState.Move && _state != BallState.Attack)
		{
			return;
		}

		_attackLeft = duration;
		ChangeState(BallState.Attack);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_state == BallState.Controlled)
		{
			TickControlled((float)delta);
			return;
		}

		if (_state == BallState.Attack)
		{
			TickAttack((float)delta);
		}
	}

	/// <summary>受控计时：时间走完自动回到移动状态。</summary>
	private void TickControlled(float delta)
	{
		_controlLeft -= delta;
		if (_controlLeft > 0f)
		{
			return;
		}

		_controlLeft = 0f;
		ChangeState(BallState.Move);
	}

	/// <summary>攻击计时：动作演完自动回到移动状态。</summary>
	private void TickAttack(float delta)
	{
		_attackLeft -= delta;
		if (_attackLeft > 0f)
		{
			return;
		}

		_attackLeft = 0f;
		ChangeState(BallState.Move);
	}

	/// <summary>切换状态的唯一入口：改状态 + 发事件，所有切换都得走这里。</summary>
	private void ChangeState(BallState next)
	{
		if (next == _state)
		{
			return;
		}

		// 死亡是终点：谁也别想把死人拉回别的状态
		if (_state == BallState.Dead)
		{
			return;
		}

		_state = next;

		// 死了就没有速度可言：面板显示、物理状态都干净
		if (next == BallState.Dead)
		{
			Velocity = Vector2.Zero;
		}

		Events.Trigger(EventName.state_changed, next);
	}

	/// <summary>
	/// 造成伤害：整条受伤链走一遍（谁先处理、谁能截断，由 `DamagePriority` 定死）。
	///
	/// 为什么伤害要过一条事件链，而不是直接扣血：一次伤害会被好几件事影响——
	/// 无敌要挡掉它、真伤要无视护盾、护盾要吃掉一部分再把剩下的往后传、持续掉血要顺带扣一点……
	/// 这些是同一条链上的不同优先级，所以事件必须能手写、必须能中途阻塞。
	///
	/// 返回值：true = 走完了（该扣的血已经扣了），false = 被某个处理函数挡下来了（比如无敌）。
	/// </summary>
	public bool TakeDamage(DamageEvent hit)
	{
		if (hit == null)
		{
			return true;
		}

		return Events.Trigger(EventName.take_damage, hit);
	}

	/// <summary>
	/// 不带来源的写法：内部包一个"只有伤害值"的事件。
	/// 带来源的（谁打的、哪种攻击）用上面那个重载，信息都在 DamageEvent 里。
	/// </summary>
	public bool TakeDamage(float damage)
	{
		return TakeDamage(new DamageEvent(this, damage));
	}
}
