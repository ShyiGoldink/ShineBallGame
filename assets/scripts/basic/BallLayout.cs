using Godot;

/// <summary>
/// 球身上那片"条"的布局：血条、护盾条、咒力条……全挂在这个节点下面，由它统一往下排。
///
/// **为什么要有它**：以前每加一根条，都得往预制体里加一个 node，再在 `BallLook` 里
/// 单独写一段摆位（血条一段、护盾条一段）。条一多，这种"一根条一份摆位"必然各摆各的、
/// 迟早叠在一起。现在只有这一个文件知道"条是怎么排的"，加新条不用动预制体。
///
/// 分工：
/// * 布局只管**排在哪儿**——从球的正下方开始，一条一条往下、每条水平居中；
/// * 每条自己管**长什么样、多大**（在自己的 `_Ready` 里设 `CustomMinimumSize`）；
/// * 整片条区的起始高度由 `BallLook.PlaceLayout` 设（要等组件的 `Bind` 跑完，
///   碰撞圈才是最终尺寸）。
///
/// 没显示出来的条（比如没护盾的球身上那根）不占地方，排的时候直接跳过——
/// 它被打开时会重新排一遍，所以位置自己就对了。
/// </summary>
public partial class BallLayout : Node2D
{
    /// <summary>两条之间的缝。</summary>
    public const float Gap = 4f;

    public override void _Ready()
    {
        // 以后在比赛中间挂上来的条（不是装配期挂的）走这条路
        ChildEnteredTree += OnChildEnteredTree;

        // 预制体里本来就有的条：这里连他们的显隐变化
        // （子节点的 _Ready 比父节点先跑，所以这一刻条的尺寸已经定好了）
        foreach (var child in GetChildren())
        {
            Watch(child);
        }

        Stack();
    }

    /// <summary>
    /// 重新排一遍：只排看得见的条，从上往下一条接一条，每条按自己的宽度水平居中
    /// （布局的原点在球心，所以居中就是往左挪半个宽度）。
    /// </summary>
    public void Stack()
    {
        float y = 0f;

        foreach (var child in GetChildren())
        {
            if (child is not Control bar || !bar.Visible)
            {
                continue; // 不是条、或者没显示：不占地方
            }

            // 尺寸由条自己定（它 _Ready 里的 CustomMinimumSize），这里只照抄过来摆放——
            // 所以新加一种条，这个文件一个字都不用改
            var size = bar.CustomMinimumSize;
            if (size.X <= 0f || size.Y <= 0f)
            {
                size = bar.Size;
            }

            var position = new Vector2(-size.X * 0.5f, y);

            // 值没变就别写：Control 改尺寸会触发一堆重排，没必要每帧都做
            if (bar.Size != size)
            {
                bar.Size = size;
            }

            if (bar.Position != position)
            {
                bar.Position = position;
            }

            y += size.Y + Gap;
        }
    }

    private void OnChildEnteredTree(Node node)
    {
        Watch(node);

        // 等它 _Ready 完再排：尺寸是那一刻才定的
        Callable.From(Stack).CallDeferred();
    }

    /// <summary>盯着一条的显隐：被打开/关掉就重新排一遍，位置自己就跟着变。</summary>
    private void Watch(Node node)
    {
        if (node is Control bar)
        {
            bar.VisibilityChanged += Stack;
        }
    }
}
