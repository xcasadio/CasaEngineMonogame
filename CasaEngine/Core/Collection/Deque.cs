
// This class is based on the work of Leslie Sanford which was made 
// available at The Code Project:
// http://www.codeproject.com/csharp/deque.asp
// It has been modified slightly for use within the Engine


/* Copyright (c) 2006 Leslie Sanford
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy 
 * of this software and associated documentation files (the "Software"), to 
 * deal in the Software without restriction, including without limitation the 
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or 
 * sell copies of the Software, and to permit persons to whom the Software is 
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in 
 * all copies or substantial portions of the Software. 
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
 * THE SOFTWARE.
 */



/*
 * Leslie Sanford
 * Email: jabberdabber@hotmail.com
 */


using System.Collections;

namespace CasaEngine.Core.Collection;

/// <summary>
/// A Generic Deque collection class for a given type T
/// The Deque collection is like a mix of a stack and a queue.
/// 
/// It allows you to treat it in either fashion, by allowing
/// PushFront/PushBack/PopFront/PopBack to be called at any point.
/// 
/// It is implemented as a double linked list, so insertion/removal
/// are O(1) and traversal is O(n)
/// </summary>
public class Deque<T> : ICollection, IEnumerable<T>, ICloneable
{

    // Represents a node in the deque.
    [Serializable()]
    private class Node
    {
        private T value;

        private Node previous = null;

        private Node next = null;

        public Node(T value)
        {
            this.value = value;
        }

        public T Value => value;

        public Node Previous
        {
            get => previous;
            set => previous = value;
        }

        public Node Next
        {
            get => next;
            set => next = value;
        }
    }



    [Serializable()]
    private class Enumerator : IEnumerator<T>
    {
        private Deque<T> owner;

        private Node currentNode;

        private T current = default;

        private bool moveResult = false;

        private long version;

        // A value indicating whether the enumerator has been disposed.
        private bool disposed = false;

        public Enumerator(Deque<T> owner)
        {
            this.owner = owner;
            currentNode = owner.front;
            version = owner.version;
        }


        public void Reset()
        {

            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            else if (version != owner.version)
            {
                throw new InvalidOperationException(
                    "The Deque was modified after the enumerator was created.");
            }


            currentNode = owner.front;
            moveResult = false;
        }

        public object Current
        {
            get
            {

                if (disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                else if (!moveResult)
                {
                    throw new InvalidOperationException(
                        "The enumerator is positioned before the first " +
                        "element of the Deque or after the last element.");
                }


                return current;
            }
        }

        public bool MoveNext()
        {

            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            else if (version != owner.version)
            {
                throw new InvalidOperationException(
                    "The Deque was modified after the enumerator was created.");
            }


            if (currentNode != null)
            {
                current = currentNode.Value;
                currentNode = currentNode.Next;

                moveResult = true;
            }
            else
            {
                moveResult = false;
            }

            return moveResult;
        }



        T IEnumerator<T>.Current
        {
            get
            {

                if (disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                else if (!moveResult)
                {
                    throw new InvalidOperationException(
                        "The enumerator is positioned before the first " +
                        "element of the Deque or after the last element.");
                }


                return current;
            }
        }



        public void Dispose()
        {
            disposed = true;
        }

    }




    // The node at the front of the deque.
    private Node front = null;

    // The node at the back of the deque.
    private Node back = null;

    // The number of elements in the deque.
    private int count = 0;

    // The version of the deque.
    private long version = 0;



    /// <summary>
    /// Initializes a new instance of the Deque class.
    /// </summary>
    public Deque()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Deque class that contains 
    /// elements copied from the specified collection.
    /// </summary>
    /// <param name="collection">
    /// The collection whose elements are copied to the new Deque.
    /// </param>
    public Deque(IEnumerable<T> collection)
    {

        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }


        foreach (T item in collection)
        {
            PushBack(item);
        }
    }



    /// <summary>
    /// Removes all objects from the Deque.
    /// </summary>
    public virtual void Clear()
    {
        count = 0;

        front = back = null;

        version++;
    }

    /// <summary>
    /// Determines whether or not an element is in the Deque.
    /// </summary>
    /// <param name="obj">
    /// The Object to locate in the Deque.
    /// </param>
    /// <returns>
    /// <b>true</b> if <i>obj</i> if found in the Deque; otherwise, 
    /// <b>false</b>.
    /// </returns>
    public virtual bool Contains(T obj)
    {
        foreach (T o in this)
        {
            if (EqualityComparer<T>.Default.Equals(o, obj))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Inserts an object at the front of the Deque.
    /// </summary>
    /// <param name="item">
    /// The object to push onto the deque;
    /// </param>
    public virtual void PushFront(T item)
    {
        // The new node to add to the front of the deque.
        Node newNode = new Node(item);

        // Link the new node to the front node. The current front node at 
        // the front of the deque is now the second node in the deque.
        newNode.Next = front;

        // If the deque isn't empty.
        if (Count > 0)
        {
            // Link the current front to the new node.
            front.Previous = newNode;
        }

        // Make the new node the front of the deque.
        front = newNode;

        // Keep track of the number of elements in the deque.
        count++;

        // If this is the first element in the deque.
        if (Count == 1)
        {
            // The front and back nodes are the same.
            back = front;
        }

        version++;
    }

    /// <summary>
    /// Inserts an object at the back of the Deque.
    /// </summary>
    /// <param name="item">
    /// The object to push onto the deque;
    /// </param>
    public virtual void PushBack(T item)
    {
        // The new node to add to the back of the deque.
        Node newNode = new Node(item);

        // Link the new node to the back node. The current back node at 
        // the back of the deque is now the second to the last node in the
        // deque.
        newNode.Previous = back;

        // If the deque is not empty.
        if (Count > 0)
        {
            // Link the current back node to the new node.
            back.Next = newNode;
        }

        // Make the new node the back of the deque.
        back = newNode;

        // Keep track of the number of elements in the deque.
        count++;

        // If this is the first element in the deque.
        if (Count == 1)
        {
            // The front and back nodes are the same.
            front = back;
        }

        version++;
    }

    /// <summary>
    /// Removes and returns the object at the front of the Deque.
    /// </summary>
    /// <returns>
    /// The object at the front of the Deque.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The Deque is empty.
    /// </exception>
    public virtual T PopFront()
    {

        if (Count == 0)
        {
            throw new InvalidOperationException("Deque is empty.");
        }


        // Get the object at the front of the deque.
        T item = front.Value;

        // Move the front back one node.
        front = front.Next;

        // Keep track of the number of nodes in the deque.
        count--;

        // If the deque is not empty.
        if (Count > 0)
        {
            // Tie off the previous link in the front node.
            front.Previous = null;
        }
        // Else the deque is empty.
        else
        {
            // Indicate that there is no back node.
            back = null;
        }

        version++;

        return item;
    }

    /// <summary>
    /// Removes and returns the object at the back of the Deque.
    /// </summary>
    /// <returns>
    /// The object at the back of the Deque.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The Deque is empty.
    /// </exception>
    public virtual T PopBack()
    {

        if (Count == 0)
        {
            throw new InvalidOperationException("Deque is empty.");
        }


        // Get the object at the back of the deque.
        T item = back.Value;

        // Move back node forward one node.
        back = back.Previous;

        // Keep track of the number of nodes in the deque.
        count--;

        // If the deque is not empty.
        if (Count > 0)
        {
            // Tie off the next link in the back node.
            back.Next = null;
        }
        // Else the deque is empty.
        else
        {
            // Indicate that there is no front node.
            front = null;
        }

        version++;


        return item;
    }

    /// <summary>
    /// Returns the object at the front of the Deque without removing it.
    /// </summary>
    /// <returns>
    /// The object at the front of the Deque.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The Deque is empty.
    /// </exception>
    public virtual T PeekFront()
    {

        if (Count == 0)
        {
            throw new InvalidOperationException("Deque is empty.");
        }


        return front.Value;
    }

    /// <summary>
    /// Returns the object at the back of the Deque without removing it.
    /// </summary>
    /// <returns>
    /// The object at the back of the Deque.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The Deque is empty.
    /// </exception>
    public virtual T PeekBack()
    {

        if (Count == 0)
        {
            throw new InvalidOperationException("Deque is empty.");
        }


        return back.Value;
    }

    /// <summary>
    /// Copies the Deque to a new array.
    /// </summary>
    /// <returns>
    /// A new array containing copies of the elements of the Deque.
    /// </returns>
    public virtual T[] ToArray()
    {
        T[] array = new T[Count];
        int index = 0;

        foreach (T item in this)
        {
            array[index] = item;
            index++;
        }

        return array;
    }




    /// <summary>
    /// Gets a value indicating whether access to the Deque is synchronized 
    /// (thread-safe).
    /// </summary>
    public virtual bool IsSynchronized => false;

    /// <summary>
    /// Gets the number of elements contained in the Deque.
    /// </summary>
    public virtual int Count => count;

    /// <summary>
    /// Copies the Deque elements to an existing one-dimensional Array, 
    /// starting at the specified array index.
    /// </summary>
    /// <param name="array">
    /// The one-dimensional Array that is the destination of the elements 
    /// copied from Deque. The Array must have zero-based indexing. 
    /// </param>
    /// <param name="index">
    /// The zero-based index in array at which copying begins. 
    /// </param>
    public virtual void CopyTo(Array array, int index)
    {

        if (array == null)
        {
            throw new ArgumentNullException(nameof(array));
        }
        else if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index,
                "Index is less than zero.");
        }
        else if (array.Rank > 1)
        {
            throw new ArgumentException("Array is multidimensional.");
        }
        else if (index >= array.Length)
        {
            throw new ArgumentException("Index is equal to or greater " +
                                        "than the length of array.");
        }
        else if (Count > array.Length - index)
        {
            throw new ArgumentException(
                "The number of elements in the source Deque is greater " +
                "than the available space from index to the end of the " +
                "destination array.");
        }


        int i = index;

        foreach (object obj in this)
        {
            array.SetValue(obj, i);
            i++;
        }
    }

    /// <summary>
    /// Gets an object that can be used to synchronize access to the Deque.
    /// </summary>
    public virtual object SyncRoot => this;


    /// <summary>
    /// Returns an enumerator that can iterate through the Deque.
    /// </summary>
    /// <returns>
    /// An IEnumerator for the Deque.
    /// </returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(this);
    }



    /// <summary>
    /// Creates a shallow copy of the Deque.
    /// </summary>
    /// <returns>
    /// A shallow copy of the Deque.
    /// </returns>
    public virtual object Clone()
    {
        Deque<T> clone = new Deque<T>(this);

        clone.version = version;

        return clone;
    }



    /// <summary>
    /// Return a generic Enumerator to Enumerate over the Deque
    /// </summary>
    public virtual IEnumerator<T> GetEnumerator()
    {
        return new Enumerator(this);
    }

}