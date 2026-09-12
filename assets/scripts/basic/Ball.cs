using Godot;
using System;

	/// <summary>
	/// 这是基类小球
	/// </summary>
public partial class Ball : CharacterBody2D
{
	/// <summary>
	/// 小的事件系统，用于事件传递
	/// </summary>
	public BallEvent Events { get; } = new();
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

	/// <summary>当前状态。私有：外面只能通过公开的切换方法改，或者订阅状态变化事件。</summary>
	private BallState _state = BallState.Spawn;

	/// <summary>受控状态的剩余时间。</summary>
	private float _controlLeft;

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

	public override void _PhysicsProcess(double delta)
	{
		if (_state != BallState.Controlled)
		{
			return;
		}

		_controlLeft -= (float)delta;
		if (_controlLeft > 0f)
		{
			return;
		}

		_controlLeft = 0f;
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
		Events.Trigger(EventName.state_changed, next);
	}

	public void TakeDamage(float damage)
	{
		/**这里应该向子组件发送状态变化信号，然后让子组件去改变。*/
		/**考虑到无敌不一定存在，而且针对血量的改变可能不止无敌，比如说持续掉血，就必须要考虑到子组件的优先级*/
		/**也就是针对同一信号，子组件不但存在优先级，而且还存在阻塞性组件*/
		/**就拿受伤来算，假设这个小球A同时存在下面的子组件：无敌，护盾，持续性扣血，一般扣血，敌对小球B的能力是真伤，所以给小球A又添加了真伤组件*/
		/**那么这个小球A应对扣血的子组件优先级就是这样的：无敌（阻塞），真伤，护盾，持续性扣血，一般扣血*/
		/**这样的话，小球A被真伤攻击打中，如果无敌，那么就阻塞，不会扣血；如果不无敌，那么就扣真伤，阻塞；如果不是真伤且不无敌，那么如果有护盾就扣护盾，护盾是非阻塞的，收到的伤害传递给持续性扣血，这个是阻塞的*/
		/**假设小球的B的能力是“沉默“，那么久是给小球B上组件沉默，沉默组件就是在无敌后面加一个沉默-扣血，这样就能直接扣血，后面的护盾等都无效了*/
		/**那么，我现在确定了两件事：第一，事件需要手写，保证事件立案中间可以阻塞；第二，需要一个静态类，用于给每个组件上优先级别，就算是并发也一定有先后顺序，所以就强制先后级别就行*/
		/**这个静态类储存的数据，就用无敌或者说绝对免疫作为第一优先级好了*/
		Events.Trigger(EventName.take_damage, damage);
	}


}
