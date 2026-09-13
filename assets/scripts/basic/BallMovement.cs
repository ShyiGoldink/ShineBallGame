using Godot;

/// <summary>
/// 球的位移：按当前速度推一步，撞到东西就按反射弹开（速度大小不变）。
///
/// 移动类组件（1001）和受控状态下接手的控制类组件（6001）用的是同一套，
/// 免得"怎么动、怎么弹"在两边各写一遍，改一处忘一处。
/// </summary>
public static class BallMovement
{
    /// <summary>按当前速度推一步，撞到就反弹。delta 是这一帧的时间。</summary>
    public static void Drive(Ball ball, float delta)
    {
        if (ball == null || delta <= 0f)
        {
            return;
        }

        var collision = ball.MoveAndCollide(ball.Velocity * delta);
        if (collision != null)
        {
            ball.Velocity = ball.Velocity.Bounce(collision.GetNormal());
        }
    }
}
