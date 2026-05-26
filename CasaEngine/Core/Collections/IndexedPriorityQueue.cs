using System.Collections;

namespace CasaEngine.Core.Collections;

/// <summary>
/// This class represents an indexed priority queue. Instead of ordering the elements themselves, the priority queue
/// holds indexes to a list that contains the true elements to order
/// </summary>
/// <remarks>
/// My own home-implementation. Seems to work, but I haven�t checked in an algorithm book other ways to implement an
/// indexed priority queue using a two-way heap (or a d-way heap)
/// </remarks>
/// <typeparam name="T">The type of the indexed elements</typeparam>
public class IndexedPriorityQueue<T> : IReadOnlyCollection<int>
{
    /// <summary>
    /// The indexed element ids in heap order.
    /// </summary>
    protected readonly List<int> HeapElements = new();

    /// <summary>
    /// The elements indexed
    /// </summary>
    protected List<T> _indexedElements = new();

    /// <summary>
    /// The comparer for the indexes
    /// </summary>
    protected IComparer<T> IndexComparer;

    /// <summary>
    /// This list gives us the index where an element from the indexedPriority list is in the heapElements list. It allows
    /// to move through the priority queue in the 2 ways (from index to indexed element and viceversa)
    /// </summary>
    protected List<int> ReversedIndexes = new();

    /// <summary>
    /// Default constructor. Uses the default comparer for the elements in the indexed priority queue
    /// </summary>
    public IndexedPriorityQueue() : this(Comparer<T>.Default) { }

    /// <summary>
    /// Creates an indexed priority queue with a specific IComparer
    /// </summary>
    /// <param name="indexComparer">The specific IComparer used to compare the indexed elements</param>
    public IndexedPriorityQueue(IComparer<T> indexComparer)
    {
        IndexComparer = indexComparer ?? throw new ArgumentNullException(nameof(indexComparer));
    }

    /// <summary>
    /// Creates an indexed priority queue with a generic comparer and with the indexed elements list
    /// </summary>
    /// <param name="indexedElements">The list where we are going to index the priority queue</param>
    public IndexedPriorityQueue(List<T> indexedElements) : this(Comparer<T>.Default)
    {
        SetIndexedElements(indexedElements);
    }

    /// <summary>
    /// Creates an indexed priority queue with a specific comparer and with the indexed elements list
    /// </summary>
    /// <param name="indexComparer">The specific IComparer used to compare the indexed elements</param>
    /// <param name="indexedElements">The list where we are going to index the priority queue</param>
    public IndexedPriorityQueue(IComparer<T> indexComparer, List<T> indexedElements) : this(indexComparer)
    {
        SetIndexedElements(indexedElements);
    }

    /// <summary>
    /// Gets or sets the indexed elements list
    /// </summary>
    public List<T> IndexedElements
    {
        get => _indexedElements;
        set
        {
            SetIndexedElements(value);
        }
    }

    /// <summary>
    /// Number of active indexes in the heap.
    /// </summary>
    public int Count => HeapElements.Count;

    /// <summary>
    /// Push an object onto the PQ
    /// </summary>
    /// <param name="element">The new object</param>
    /// <returns>The index in the list where the object is _now_. This will change when objects are taken from or put onto the PQ</returns>
    public int Enqueue(int element)
    {
        ValidateIndexedElement(element);
        EnsureReverseIndexCount();
        if (ReversedIndexes[element] != -1)
        {
            throw new InvalidOperationException("IndexedPriorityQueue: element is already enqueued.");
        }

        int p = HeapElements.Count;
        HeapElements.Add(element);
        ReversedIndexes[element] = p;
        return HeapifyUp(p);
    }

    /// <summary>
    /// Get the smallest object and remove it
    /// </summary>
    /// <returns>The smallest object</returns>
    public int Dequeue()
    {
        if (HeapElements.Count == 0)
        {
            throw new InvalidOperationException("IndexedPriorityQueue: queue is empty.");
        }

        //Get the smallest element
        int result = HeapElements[0];
        ReversedIndexes[result] = -1;

        int lastIndex = HeapElements.Count - 1;
        if (lastIndex == 0)
        {
            HeapElements.RemoveAt(lastIndex);
            return result;
        }

        HeapElements[0] = HeapElements[lastIndex];
        ReversedIndexes[HeapElements[0]] = 0;
        HeapElements.RemoveAt(HeapElements.Count - 1);
        HeapifyDown(0);

        return result;
    }

    /// <summary>
    /// Get the smallest object without removing it.
    /// </summary>
    /// <returns>The smallest object, or 0 when the queue is empty.</returns>
    public int Peek()
    {
        if (HeapElements.Count > 0)
        {
            return HeapElements[0];
        }

        return default;
    }

    /// <summary>
    /// Swaps two elements and the indexed elements
    /// </summary>
    /// <param name="i">The first index to swap</param>
    /// <param name="j">The second index to swap</param>
    protected virtual void Swap(int i, int j)
    {
        int firstElement = HeapElements[i];
        int secondElement = HeapElements[j];
        HeapElements[i] = secondElement;
        HeapElements[j] = firstElement;
        ReversedIndexes[firstElement] = j;
        ReversedIndexes[secondElement] = i;
    }

    /// <summary>
    /// Compares two indexed elements
    /// </summary>
    /// <param name="i">The first index to compare</param>
    /// <param name="j">The second index compare</param>
    protected virtual int Compare(int i, int j)
    {
        return IndexComparer.Compare(_indexedElements[HeapElements[i]], _indexedElements[HeapElements[j]]);
    }



    /// <summary>
    /// Indicates the indexed priority queue that we have changed the value of the indexed element i,
    /// and that it should update the heap
    /// </summary>
    /// <param name="i">The element we have updated</param>
    public void ChangePriority(int i)
    {
        ValidateIndexedElement(i);
        EnsureReverseIndexCount();
        int heapIndex = ReversedIndexes[i];
        if (heapIndex == -1)
        {
            throw new InvalidOperationException("IndexedPriorityQueue: element is not enqueued.");
        }

        Update(heapIndex);
    }

    /// <summary>
    /// Indicates if the indexed element is currently enqueued.
    /// </summary>
    /// <param name="element">The indexed element to test.</param>
    public bool Contains(int element)
    {
        return element >= 0 && element < ReversedIndexes.Count && ReversedIndexes[element] != -1;
    }

    /// <summary>
    /// Removes all active indexes from the heap.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < HeapElements.Count; i++)
        {
            ReversedIndexes[HeapElements[i]] = -1;
        }

        HeapElements.Clear();
    }

    public IEnumerator<int> GetEnumerator()
    {
        return HeapElements.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    protected virtual void Update(int i)
    {
        int newIndex = HeapifyUp(i);
        if (newIndex == i)
        {
            HeapifyDown(i);
        }
    }

    private void SetIndexedElements(List<T> indexedElements)
    {
        _indexedElements = indexedElements ?? throw new ArgumentNullException(nameof(indexedElements));
        HeapElements.Clear();
        HeapElements.Capacity = indexedElements.Count;
        ReversedIndexes.Clear();
        for (int i = 0; i < indexedElements.Count; i++)
        {
            ReversedIndexes.Add(-1);
        }
    }

    private int HeapifyUp(int index)
    {
        int p = index;
        while (p > 0)
        {
            int parent = (p - 1) / 2;
            if (Compare(p, parent) >= 0)
            {
                break;
            }

            Swap(p, parent);
            p = parent;
        }

        return p;
    }

    private int HeapifyDown(int index)
    {
        int p = index;
        while (true)
        {
            int next = p;
            int left = 2 * p + 1;
            int right = 2 * p + 2;

            if (HeapElements.Count > left && Compare(next, left) > 0)
            {
                next = left;
            }

            if (HeapElements.Count > right && Compare(next, right) > 0)
            {
                next = right;
            }

            if (next == p)
            {
                return p;
            }

            Swap(next, p);
            p = next;
        }
    }

    private void ValidateIndexedElement(int element)
    {
        if (element < 0 || element >= _indexedElements.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(element), "IndexedPriorityQueue: element index is outside indexed elements.");
        }
    }

    private void EnsureReverseIndexCount()
    {
        for (int i = ReversedIndexes.Count; i < _indexedElements.Count; i++)
        {
            ReversedIndexes.Add(-1);
        }
    }
}