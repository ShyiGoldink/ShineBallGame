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
			float before = _hp;
			_hp = value;
			RefreshHealthBar();

			// 血扣光了就进死亡状态。NormalMove 只在移动状态里动，所以球会自己停下来。
			if (_hp <= 0f)
			{
				ChangeState(BallState.Dead);
			}

			// 血量**真的变了**才喊一声（载荷是这次变了多少：正数回血、负数掉血）。
			// 想"只在受伤 / 回血时做点什么"的组件订这个事件，别每帧去比血量。
			// 放到死亡判定后面：订阅者拿到通知时，状态已经是最终的了。
			if (_hp != before)
			{
				Events.Trigger(EventName.hp_changed, _hp - before);
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

	/// <summary>
	/// 把当前血量推给身上的血条；身上没有血条就什么都不做。
	/// 血条挂在条区（`BallLayout`）下面，所以要问条区要，不能只扫自己的直接子节点。
	/// </summary>
	private void RefreshHealthBar()
	{
		_healthBar ??= BallLook.Layout(this)?.GetNodeOrNull<HealthBar>(BallLook.HealthBarName);
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
	/// 这会儿在演哪一招（空字符串 = 没在攻击）。面板、动画、日志都看它——
	/// 光靠"状态是攻击"分不出是哪个招式。
	/// </summary>
	public string MoveId { get; private set; } = string.Empty;

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

	/// <summary>
	/// 提前结束受控：受控 → 移动。
	/// 谁把人放开的谁负责喊这一句（`8002 domain.control` 配了 `release_when_free` 时就这么用）。
	/// 不在受控状态里调用等于什么都没做。
	/// </summary>
	public void EndControl()
	{
		if (_state == BallState.Controlled)
		{
			_controlLeft = 0f;
			ChangeState(BallState.Move);
		}
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
	/// 只有移动中（或者本来就在攻击中，用来换招/续时间）能进；
	/// 登场、受控、死亡都不接受——攻击要不要在这些状态下强行插队，等玩法定了再说。
	///
	/// 已经在攻击中时，`repeat` 决定这一招跟当前那一招怎么处（覆盖 / 延长 / 打断，见 `AttackRepeat`）；
	/// 刚进攻击（从移动状态）时它没有区别，三种都是"开始演这一招"。
	/// </summary>
	public void BeginAttack(string moveId, float duration, AttackRepeat repeat = AttackRepeat.Replace)
	{
		if (_state == BallState.Dead)
		{
			return;
		}

		if (_state != BallState.Move && _state != BallState.Attack)
		{
			return;
		}

		// 延长：招式不变，只把时间加上去（动画继续播，不重播）
		if (_state == BallState.Attack && repeat == AttackRepeat.Extend)
		{
			_attackLeft += Mathf.Max(duration, 0f);
			return;
		}

		// 覆盖 / 打断：当前这一招到此为止。打断会额外喊一声，
		// 让那一招有机会收尾（比如取消还没生效的前摇）；覆盖则当作正常换招，不喊。
		if (_state == BallState.Attack && repeat == AttackRepeat.Interrupt)
		{
			Events.Trigger(EventName.attack_interrupted, MoveId);
		}

		_attackLeft = duration;
		MoveId = moveId ?? string.Empty;
		ChangeState(BallState.Attack);

		// 出招的开机通知：外观按招式名切动画（本来就靠这个知道"是哪一招"）
		Events.Trigger(EventName.attack_started, MoveId);
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
			return;
		}

		// 移动：把"这一帧谁来驱动我"交给移动链（谁先处理谁说话，返回 false 就阻塞后面的）。
		// 只在移动状态发，所以登场/受控/攻击/死亡时谁都叫不起来。
		if (_state == BallState.Move)
		{
			Events.Trigger(EventName.move_step, (float)delta);
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

		// 离开攻击状态就把招式名清掉
		if (next != BallState.Attack)
		{
			MoveId = string.Empty;
		}

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
