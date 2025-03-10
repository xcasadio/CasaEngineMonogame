﻿namespace CasaEngine.Core.Design;

public class EventArgs<T> : EventArgs
{
    public T Value { get; }

    public EventArgs(T value)
    {
            Value = value;
        }
}

public class EventArgs<T, TU> : EventArgs
{
    public T Value { get; }
    public TU Value2 { get; }

    public EventArgs(T value, TU value2)
    {
            Value = value;
            Value2 = value2;
        }
}

public class EventArgs<T, TU, TV> : EventArgs
{
    public T Value { get; }
    public TU Value2 { get; }
    public TV Value3 { get; }

    public EventArgs(T value, TU value2, TV value3)
    {
            Value = value;
            Value2 = value2;
            Value3 = value3;
        }
}