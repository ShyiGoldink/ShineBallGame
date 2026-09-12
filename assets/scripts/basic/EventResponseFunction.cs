using System;

public struct EventResponseFunction
{
    public int priority;
    public Func<object, bool> action;
}
