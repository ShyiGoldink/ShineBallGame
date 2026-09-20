using Godot;

/// <summary>
/// 普通移动：给一个初速度和一个初始方向，之后一直匀速直线前进，
/// 撞到东西就按反射（相对碰撞法线对称）弹开，速度大小不变。
///
/// 它**不自己跑 `_PhysicsProcess`**，而是挂在"移动链"上（见 `MovePriority`）、优先级最低：
/// 球每帧在移动状态发一次 `move_step`，苍/赫 那种要接管的东西排在它前面，
/// 人家处理完返回 false 就把它挡掉了——这就是"被阻塞"，它自己不用知道有谁在场。
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

    public override void ApplyParams(Godot.Collections.Dictionary parameters)
    {
        Speed = JsonTool.GetValue(parameters, "speed", Speed);
    }

    /// <summary>
    /// 装配时接线：把自己挂到移动链的最后（优先级最低）。
    /// "只在移动状态动"不用自己判断了——移动链只在移动状态发。
    /// </summary>
    public override void Bind(Ball ball, string configId)
    {
        _ball = ball;
        if (ball == null)
        {
            GD.PushError($"[{Type}] 组件没挂在球下面，没法移动。");
            return;
        }

        _ball.Events.Register(EventName.move_step, new EventResponseFunction
        {
            priority = MovePriority.Normal,
            action = OnMoveStep,
        });
    }

    /// <summary>移动链轮到我了：推一步。返回 false —— 它是链条的收尾，处理完就截断。</summary>
    private bool OnMoveStep(object arg)
    {
        if (_ball == null || arg is not float delta)
        {
            return true;
        }

        // 第一帧才把初速度写进去：工厂可能是先挂组件、后填参数，
        // 这样无论填参数在哪一步，写进去的都是最终值。
        if (!_started)
        {
            _ball.Velocity = PickDirection() * Speed;
            _started = true;
        }

        // 推进 + 反弹：跟受控状态下的控制类组件共用一套（BallMovement）
        BallMovement.Drive(_ball, delta);
        return false;
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
