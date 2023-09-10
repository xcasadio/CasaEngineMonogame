using System;
using System.Collections.Generic;

namespace TomShane.Neoforce.Controls;

public class EventedList<T> : List<T>
{

    public event EventHandler ItemAdded;
    public event EventHandler ItemRemoved;

    public EventedList() : base() { }
    public EventedList(int capacity) : base(capacity) { }
    public EventedList(IEnumerable<T> collection) : base(collection) { }

    public new void Add(T item)
    {
        var c = Count;
        base.Add(item);
        if (ItemAdded != null && c != Count)
        {
            ItemAdded.Invoke(this, new EventArgs());
        }
    }

    public new void Remove(T obj)
    {
        var c = Count;
        base.Remove(obj);
        if (ItemRemoved != null && c != Count)
        {
            ItemRemoved.Invoke(this, new EventArgs());
        }
    }

    public new void Clear()
    {
        var c = Count;
        base.Clear();
        if (ItemRemoved != null && c != Count)
        {
            ItemRemoved.Invoke(this, new EventArgs());
        }
    }

    public new void AddRange(IEnumerable<T> collection)
    {
        var c = Count;
        base.AddRange(collection);
        if (ItemAdded != null && c != Count)
        {
            ItemAdded.Invoke(this, new EventArgs());
        }
    }

    public new void Insert(int index, T item)
    {
        var c = Count;
        base.Insert(index, item);
        if (ItemAdded != null && c != Count)
        {
            ItemAdded.Invoke(this, new EventArgs());
        }
    }

    public new void InsertRange(int index, IEnumerable<T> collection)
    {
        var c = Count;
        base.InsertRange(index, collection);
        if (ItemAdded != null && c != Count)
        {
            ItemAdded.Invoke(this, new EventArgs());
        }
    }

    public new int RemoveAll(Predicate<T> match)
    {
        var c = Count;
        var ret = base.RemoveAll(match);
        if (ItemRemoved != null && c != Count)
        {
            ItemRemoved.Invoke(this, new EventArgs());
        }

        return ret;
    }

    public new void RemoveAt(int index)
    {
        var c = Count;
        base.RemoveAt(index);
        if (ItemRemoved != null && c != Count)
        {
            ItemRemoved.Invoke(this, new EventArgs());
        }
    }

    public new void RemoveRange(int index, int count)
    {
        var c = Count;
        base.RemoveRange(index, count);
        if (ItemRemoved != null && c != Count)
        {
            ItemRemoved.Invoke(this, new EventArgs());
        }
    }

}