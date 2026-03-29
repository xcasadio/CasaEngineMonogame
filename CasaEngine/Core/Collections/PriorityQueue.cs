using System.Collections;

namespace CasaEngine.Core.Collections;

/// <summary>
/// This class represents a priority queue using a heap
/// </summary>
/// <remarks>
/// This class is based in the great PriorityQueue implementation from BenDi at CodeProject
/// (http://www.codeproject.com/csharp/PriorityQueue.asp)
/// </remarks>
/// <typeparam name="T">The type of the elements in the priority queue</typeparam>
public class PriorityQueue<T> : IPriorityQueue<T>
{

    /// <summary>
    /// The elements in the heap
    /// </summary>
    protected List<T> HeapElements = new();

    /// <summary>
    /// Used to compare elements when reordering the heap
    /// </summary>
    protected IComparer<T> Comparer;

    /// <summary>
    /// Default constructor. Uses the default comparer for the elements in the priority queue
    /// </summary>
    public PriorityQueue() : this(Comparer<T>.Default) { }

    /// <summary>
    /// Creates a priority queue with a specific IComparer
    /// </summary>
    /// <param name="comparer">The specific IComparer used used to compare elements</param>
    public PriorityQueue(IComparer<T> comparer)
    {
        Comparer = comparer;
    }

    /// <summary>
    /// Creates a priority queue with a default capacity and the generic comparer for T
    /// </summary>
    /// <param name="capacity">The initial capacity of the queue</param>
    public PriorityQueue(int capacity) : this(Comparer<T>.Default, capacity) { }

    /// <summary>
    /// Creates a priority queue with a default capacity and a specific comparer for T
    /// </summary>
    /// <param name="comparer">The specific IComparer used to compare elements</param>
    /// <param name="capacity">The initial capacity of the queue</param>
    public PriorityQueue(IComparer<T> comparer, int capacity)
    {
        Comparer = comparer;
        HeapElements.Capacity = capacity;
    }

    /// <summary>
    /// Push an object onto the PQ
    /// </summary>
    /// <param name="element">The new object</param>
    /// <returns>The index in the list where the object is _now_. This will change when objects are taken from or put onto the PQ</returns>
    public virtual int Enqueue(T element)
    {
        int p, p2;

        p = HeapElements.Count;
        HeapElements.Add(element);

        //Heapify up
        do
        {
            if (p == 0)
            {
                break;
            }

            p2 = (p - 1) / 2;

            if (Compare(p, p2) < 0)
            {
                Swap(p, p2);
                p = p2;
            }

            else
            {
                break;
            }
        } while (true);

        return p;
    }

    /// <summary>
    /// Get the smallest object and remove it
    /// </summary>
    /// <returns>The smallest object</returns>
    public virtual T Dequeue()
    {
        T result;
        int p, p1, p2, pn;

        if (HeapElements.Count == 0)
        {
            return default;
        }

        //Get the smallest element
        result = HeapElements[0];

        //Heapify down
        p = 0;
        HeapElements[0] = HeapElements[HeapElements.Count - 1];
        HeapElements.RemoveAt(HeapElements.Count - 1);

        do
        {
            pn = p;
            p1 = 2 * p + 1;
            p2 = 2 * p + 2;

            if (HeapElements.Count > p1 && Compare(p, p1) > 0)
            {
                p = p1;
            }

            if (HeapElements.Count > p2 && Compare(p, p2) > 0)
            {
                p = p2;
            }

            if (p == pn)
            {
                break;
            }

            Swap(p, pn);
        } while (true);

        return result;
    }

    /// <summary>
    /// Get the smallest object without removing it
    /// </summary>
    /// <returns>The smallest object</returns>
    public T Peek()
    {
        if (HeapElements.Count > 0)
        {
            return HeapElements[0];
        }

        return default;
    }

    /// <summary>
    /// Swaps two elements
    /// </summary>
    /// <param name="i">The index of the first element to swap</param>
    /// <param name="j">The index of the second element to swap</param>
    protected virtual void Swap(int i, int j)
    {
        T h;

        h = HeapElements[i];
        HeapElements[i] = HeapElements[j];
        HeapElements[j] = h;
    }

    /// <summary>
    /// Compares two elements
    /// </summary>
    /// <param name="i">The index of the first element to compare</param>
    /// <param name="j">The index of the first element to compare</param>
    /// <returns>The result of the compare method</returns>
    protected virtual int Compare(int i, int j)
    {
        return Comparer.Compare(HeapElements[i], HeapElements[j]);
    }

    /// <summary>
    /// Notify the PQ that the object at position i has changed and the PQ needs to restore order.
    /// Since you dont have access to any indexes (except by using the explicit IList.this) you should 
    /// not call this function without knowing exactly what you do
    /// </summary>
    /// <param name="i">The index of the changed object</param>
    protected virtual void Update(int i)
    {
        int p, pn;
        int p1, p2;

        //Heapify up
        p = i;
        do
        {
            if (p == 0)
            {
                break;
            }

            p2 = (p - 1) / 2;

            if (Compare(p, p2) < 0)
            {
                Swap(p, p2);
                p = p2;
            }

            else
            {
                break;
            }
        } while (true);

        if (p < i)
        {
            return;
        }

        //Heapify down
        do
        {
            pn = p;
            p1 = 2 * p + 1;
            p2 = 2 * p + 2;

            if (HeapElements.Count > p1 && Compare(p, p1) > 0)
            {
                p = p1;
            }

            if (HeapElements.Count > p2 && Compare(p, p2) > 0)
            {
                p = p2;
            }

            if (p == pn)
            {
                break;
            }

            Swap(p, pn);
        } while (true);
    }

    /// <summary>
    /// Returns an enumerator that iterates the priority queue
    /// </summary>
    /// <returns>The enumerator used to iterate the priority queue</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return HeapElements.GetEnumerator();
    }

    /// <summary>
    /// Not implemented
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotSupportedException("You can´t iterate through the elements of a priority queue using a non-generic enumerator");
    }

    /// <summary>
    /// Number of elements in the heap
    /// </summary>
    public int Count => HeapElements.Count;

    /// <summary>
    /// Indicates if the collection is readonly. Always returns false
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Indicates if the PQ contais an element
    /// </summary>
    /// <param name="value">Value we want to test</param>
    /// <returns>True if the value was in the PQ, false otherwise</returns>
    public bool Contains(T value)
    {
        return HeapElements.Contains(value);
    }

    /// <summary>
    /// Removes all elements from the heap
    /// </summary>
    public void Clear()
    {
        HeapElements.Clear();
    }

    /// <summary>
    /// Copies the elements of the heap into an array
    /// </summary>
    /// <param name="array">The source array where we are going to copy the heap elements</param>
    /// <param name="arrayIndex">The index to start copying the elements</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        HeapElements.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Adds an element to the heap
    /// </summary>
    /// <param name="item">The element to add</param>
    public void Add(T item)
    {
        Enqueue(item);
    }

    /// <summary>
    /// Not supported
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Remove(T item)
    {
        throw new NotSupportedException("You should use the method Dequeue to eliminate elements from a priority queue");
    }

    /// <summary>
    /// Searches for the element in the heap
    /// </summary>
    /// <param name="item">The element we want to search</param>
    /// <returns>The index of the element</returns>
    public int IndexOf(T item)
    {
        return HeapElements.IndexOf(item);
    }

    /// <summary>
    /// Not supported
    /// </summary>
    /// <param name="index"></param>
    /// <param name="item"></param>
    public void Insert(int index, T item)
    {
        throw new NotSupportedException("You can´t insert an element directly in a priority queue. Use Enqueue instead");
    }

    /// <summary>
    /// Not supported
    /// </summary>
    /// <param name="index"></param>
    public void RemoveAt(int index)
    {
        throw new NotSupportedException("You can´t remove an element directly from a priority queue. Use Dequeue instead");
    }

    /// <summary>
    /// Gets or sets and element directly in the priority queue
    /// </summary>
    /// <param name="index">The index we want to get or set</param>
    /// <returns>The element on the index</returns>
    public T this[int index]
    {
        get => HeapElements[index];

        set
        {
            HeapElements[index] = value;
            Update(index);
        }
    }

    /// <summary>
    /// Creates a clone from this object
    /// </summary>
    /// <returns>The clone of the object</returns>
    public object Clone()
    {
        throw new NotImplementedException();
    }
}