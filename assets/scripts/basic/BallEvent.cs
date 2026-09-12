using System;
using System.Collections.Generic;

/**
这是一个轻量化的事件中心，为了应对同一事件，按序执行，并且可以阻塞
*/
public class BallEvent
{
    /*
    事件核心的数据结构，string为响应的事件类型，使用list来排序EventResponseFunction
    */
    public Dictionary<string, List<EventResponseFunction>> EventTable { get; } = new();

/*
根据事件名称来注册事件，每次注册之后都应该根据Event Response Function的priority进行排序，从大到小
*/
    public void Register(string eventName, EventResponseFunction handler)
    {
        if (string.IsNullOrEmpty(eventName) || handler.action == null)
        {
            return;
        }

        if (!EventTable.TryGetValue(eventName, out var handlers))
        {
            handlers = new List<EventResponseFunction>();
            EventTable[eventName] = handlers;
        }

        // 按 priority 从大到小排列，数值大的先执行。
        // 这里用插入而不是每次 Sort：同样保持有序，但 Sort 是不稳定排序，
        // 插入能让同优先级的处理函数保持注册顺序，行为可预期。
        int index = handlers.Count;
        while (index > 0 && handlers[index - 1].priority < handler.priority)
        {
            index--;
        }
        handlers.Insert(index, handler);
    }

/*
注销事件：把之前注册过的处理函数从事件表里摘掉，成功返回true，没找到返回false
注意：注销必须传“注册时用的那个委托”，不能在现场重新写一个 lambda，
因为每次写 lambda 都会生成一个新对象，比对不上（方法组 this.OnHurt 这种是可以的）
同一个处理函数注册了多次时，每次调用只摘掉一个
*/
    public bool Unregister(string eventName, EventResponseFunction handler)
    {
        if (string.IsNullOrEmpty(eventName) || handler.action == null)
        {
            return false;
        }

        if (!EventTable.TryGetValue(eventName, out var handlers))
        {
            return false;
        }

        for (int i = 0; i < handlers.Count; i++)
        {
            // 委托之间用 == 比较，比的是“目标对象 + 方法”，能准确找回同一个处理函数
            if (handlers[i].action != handler.action)
            {
                continue;
            }

            handlers.RemoveAt(i);
            if (handlers.Count == 0)
            {
                // 没有监听者了就把整条记录删掉，别留下空壳
                EventTable.Remove(eventName);
            }
            return true;
        }

        return false;
    }

/*
触发器，通过传入的事件名来找到需要执行的list，arg可以是任意数据类型，甚至可以是匿名函数
接下来按需执行list，如果返回值为true那么继续执行下去，false则不会继续执行，直接返回
*/
    public bool Trigger(string eventName, object arg)
    {
        if (!EventTable.TryGetValue(eventName, out var handlers))
            return true;

        // 先取一份快照再按顺序执行：处理函数如果在执行过程中注册或注销了事件，
        // 也不会让本次执行错位（用下标直接遍历原 list 的话，前面插入 / 删除会导致
        // 后面的处理函数漏跑或重复跑）。代价是每次触发一份小分配，
        // 我们的单个事件监听者很少，可以忽略；真觉得贵再换成复用缓冲区。
        var snapshot = handlers.ToArray();
        foreach (var handler in snapshot)
        {
            if (!handler.action(arg))
                return false; // 阻塞
        }
        return true;
    }
}
