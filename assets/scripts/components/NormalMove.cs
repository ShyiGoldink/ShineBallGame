using Godot;

/// <summary>
/// 普通移动：给一个初速度和一个初始方向，之后一直匀速直线前进，
/// 撞到东西就按反射（相对碰撞法线对称）弹开，速度大小不变。
/// </summary>
public partial class NormalMove : BallComponent
{
    public override int Id => 1001;

    public override string Type => "movement.normal";

    public override string Description => "初速度 + 初始方向，撞到东西按反射弹开，速度不衰减";

    /// <summary>初速度，像素/秒。</summary>
    public float Speed = 200f;

    /// <summary>
    /// 初始方向。只取方向不看长度；留空（零向量）时开场随机挑一个斜方向，
    /// 免得两颗球都在水平或垂直线上跑，永远撞不到。
    /// </summary>
    public Vector2 Direction = Vector2.Zero;

    private Ball _ball;
    private bool _started;
    private bool _moving;

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Speed = JsonTool.GetValue(parameters, "speed", Speed);
    }

    public override void _Ready()
    {
        _ball = GetParent() as Ball;
        if (_ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，没法移动。");
            return;
        }

        // 状态一变球就通知我：只在移动状态里动，其它状态（登场、受控、攻击、死亡）都停住。
        // 优先级这里填 0 就行，状态事件没有先后之争；返回 true 是为了不挡住别的组件收听。
        _ball.Events.Register(EventName.state_changed, new EventResponseFunction { priority = 0, action = OnStateChanged });

        // 先按当前状态对一次表：球挂上组件之前可能已经切过状态了（比如刚出生的蛋），
        // 那次通知注册事件是收不到的。
        _moving = _ball.State == BallState.Move;
    }

    private bool OnStateChanged(object arg)
    {
        _moving = arg is BallState state && state == BallState.Move;
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_ball == null || !_moving)
        {
            return;
        }

        // 第一帧才把初速度写进去：工厂可能是先挂组件、后填参数，
        // 这样无论填参数在哪一步，写进去的都是最终值。
        if (!_started)
        {
            _ball.Velocity = PickDirection() * Speed;
            _started = true;
        }

        // 推进 + 反弹：跟受控状态下的控制类组件共用一套（BallMovement）
        BallMovement.Drive(_ball, (float)delta);
    }

    /// <summary>Json 里写了方向就用写的，没写就随机给一个斜方向（两个分量都不会太小）。</summary>
    private Vector2 PickDirection()
    {
        if (Direction.LengthSquared() > 0f)
        {
            return Direction.Normalized();
        }

        float angle = Mathf.DegToRad(20f + GD.Randf() * 50f);
        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        // 随机挑一个象限，四个方向都可能
        int quadrant = (int)(GD.Randi() % 4);
        if (quadrant is 1 or 2)
        {
            direction.X = -direction.X;
        }

        if (quadrant is 2 or 3)
        {
            direction.Y = -direction.Y;
        }

        return direction;
    }
}
