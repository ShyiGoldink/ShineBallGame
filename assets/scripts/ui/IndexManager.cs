using Godot;
using System.Collections.Generic;

/// <summary>
/// 开始菜单的场景管理器。
///
/// 同一个场景里放着若干页面（现在有 Index 和 Select），切换页面就是遍历列表：
/// 把不是目标的页面关掉，把目标的打开。
///
/// 约定：这个节点的直接子节点就是页面，页面 id 用节点名。
/// 所以场景里叫 Select 的节点，切换时写 Open("Select")；想加页面就加一个子节点，不用改代码。
///
/// 关于"用引擎编号管理"：Godot 确实有 Node.GetInstanceId()，但那是运行时才分配的，
/// 没法在场景里预先写死，编辑器里也指定不了，所以这里用自己定的 id（节点名）当编号。
/// </summary>
public partial class IndexManager : Control
{
    /// <summary>一个页面：自己定的 id + 页面节点。</summary>
    private struct Page
    {
        public string Id;
        public CanvasItem Node;

        public Page(string id, CanvasItem node)
        {
            Id = id;
            Node = node;
        }
    }

    private readonly List<Page> _pages = new();
    private string _currentId = string.Empty;

    /// <summary>当前页面的 id。还没切换过时是空字符串。</summary>
    public string CurrentId => _currentId;

    public override void _Ready()
    {
        // 兜底：不管从哪条路进来的，主界面都不该是暂停状态
        GetTree().Paused = false;

        // 直接子节点全部登记成页面，场景里第一个页面就是打开时的初始页。
        foreach (var child in GetChildren())
        {
            if (child is CanvasItem page)
            {
                Register(page.Name.ToString(), page);
            }
            else
            {
                GD.PushWarning($"[菜单] 子节点 {child.Name} 不是可见节点，没登记。");
            }
        }

        if (_pages.Count > 0)
        {
            Open(_pages[0].Id);
        }
    }

    /// <summary>登记一个页面，之后就能用这个 id 切过去。id 重复会拦下来。</summary>
    public void Register(string id, CanvasItem page)
    {
        if (string.IsNullOrEmpty(id) || page == null)
        {
            GD.PushError("[菜单] 登记页面失败：id 或节点为空。");
            return;
        }

        if (HasPage(id))
        {
            GD.PushError($"[菜单] 页面 id 重复：{id}");
            return;
        }

        _pages.Add(new Page(id, page));
    }

    /// <summary>切到 id 对应的页面：其它页面关掉，这个打开。</summary>
    public void Open(string id)
    {
        if (!HasPage(id))
        {
            GD.PushError($"[菜单] 没有叫 '{id}' 的页面。");
            return;
        }

        foreach (var page in _pages)
        {
            bool active = page.Id == id;

            // 关掉 = 看不见 + 不再处理输入和每帧逻辑。
            // 只设 Visible 是不够的：页面里的按钮照样收得到输入，脚本也照样在跑。
            page.Node.Visible = active;
            page.Node.ProcessMode = active ? Node.ProcessModeEnum.Inherit : Node.ProcessModeEnum.Disabled;
        }

        _currentId = id;
        GD.Print($"[菜单] 切到 {id}");
    }

    public bool HasPage(string id)
    {
        foreach (var page in _pages)
        {
            if (page.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    // ---------- 给按钮用的入口，场景里的信号就是连到这两个方法上的 ----------

    public void ShowIndex() => Open("Index");

    public void ShowSelect() => Open("Select");

    /// <summary>
    /// 退出游戏。主菜单上那个"退出游戏"按钮、以及主菜单上按 Esc，都走这里。
    /// （窗口是无边框的、没有关闭按钮，所以游戏里必须自己留一条退出的路。）
    /// </summary>
    public void QuitGame()
    {
        GD.Print("[菜单] 退出游戏");
        GetTree().Quit();
    }

    /// <summary>
    /// Esc：在子页面（选球）先回主菜单；已经在主菜单了就直接退出游戏。
    /// 用 _UnhandledInput，所以页面上正常用键盘操作的东西（按钮之类）不受影响。
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo || key.Keycode != Key.Escape)
        {
            return;
        }

        GetViewport().SetInputAsHandled();

        if (CurrentId == "Select")
        {
            ShowIndex();
        }
        else
        {
            QuitGame();
        }
    }
}
