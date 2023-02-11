using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngineCommon.Pool
{
    /// <summary>
    /// A pool of elements of reference type T.
    /// This type of pool avoid the accessor with the cost of an O(n) release operation.
    /// The accessor for reference type is needed to destroy the element and to access the pointer.
    /// This structure could only be needed in very specific scenarios.
    /// </summary>
    /// <remarks>
    /// This is a custom structure that pursues two specific objectives.
    /// The first and more obvious is to reduce garbage collector frequency.
    /// If almost all reference type fields are allocated beforehand then few news are need it and so the garbage collector will remain idle during execution.
    /// The second objective is to guarantied memory locality so that the access time could be improved because there will be less cache misses.
    /// Plenty of information is read every frame and in chunks (thanks to the component model) so this feature could be critical.
    /// This structure also offers the possibility of modified directly the data stored (to avoid replication) and add and remove elements in O(1).
    /// Another feature added was the possibility of increment the size of the pool. This operation has O(n) so avoid using it.
    /// 
    /// Important: The .NET runtime environment manages where to allocate new reference fields and we can do much about it. 
    /// However, if we create everything at the beginning when little or no fragmentation exists then it could be possible that the reference data will be together. 
    /// Of course, this could be change or could be not true even at the beginning.
    /// 
    /// This data structure exposes some bad code practice for the sake of performance. I personally don’t like to do this, but this structure is critical.
    /// 
    /// Optimization: some int values could be transformed to short.
    /// </remarks>
    /// <typeparam name="T">Valid for value or reference type</typeparam>
    public class PoolWithoutAccessor<T> where T : new()
    {


        /// <summary>
        /// The elements.
        /// </summary>
        /// <remarks>
        /// You can access directly using the accessor's index field.
        /// This is intended to avoid the replication of value types and to allow modifications in their values.
        /// </remarks>
        public T[] Elements;



        /// <summary>
        /// Gets the number of active elements in the pool.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Gets or sets the total number of elements the internal data structure can hold without resizing.
        /// </summary>
        public int Capacity
        {
            get { return Elements.Length; }
            set
            {
                if (value < Count)
                {
                    throw new ArgumentOutOfRangeException("value", "Pool: new size has to be bigger than active elements.");
                }

                ResizePool(value);
            }
        } // Capacity



        /// <summary>
        /// A pool of elements T.
        /// </summary>
        /// <param name="capacity">The total number of elements the internal data structure can hold without resizing.</param>
        public PoolWithoutAccessor(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException("capacity", "Pool: Argument capacity must be greater than zero.");
            }

            Elements = new T[capacity];
            for (int i = 0; i < capacity; i++)
            {
                // If T is a reference type then the explicit creation is need it.
                Elements[i] = new T();
            }
        } // Pool



        /// <summary>
        /// Marks an element for using it and return its corresponded accessor.
        /// </summary>
        /// <returns>The key to access the element. The element is not returned because was stored using a value type.</returns>
        public T Fetch()
        {
            if (Count >= Capacity)
            {
                ResizePool(Capacity + 25);
            }
            Count++;
            return Elements[Count - 1];
        } // Fetch



        /// <summary>
        /// Resize the pool.
        /// </summary>
        private void ResizePool(int newCapacity)
        {
            int oldCapacity = Capacity;
            T[] newElements = new T[newCapacity];
            // If T is a reference type then the explicit creation is need it.
            for (int i = 0; i < newCapacity; i++)
            {
                if (i < oldCapacity)
                {
                    newElements[i] = Elements[i];
                }
                else
                {
                    newElements[i] = new T();
                }
            }
            Elements = newElements;
        } // ResizePool



        /// <summary>
        /// Set the pool element to available.
        /// </summary>
        /// <remarks>This is a potentially an O(n) operation.</remarks>
        public void Release(T element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element", "Pool: Element value cannot be null");
            }

            // To accomplish our second objective (memory locality) the last available element will be moved to the place where the released element resided.
            int i = 0;
            while (i < Count && !Elements[i].Equals(element))
            {
                i++;
            }
            if (i == Count)
            {
                throw new ArgumentNullException("element", "Pool: Element value not found.");
            }

            T temp = Elements[i];
            Elements[i] = Elements[Count - 1];
            Elements[Count - 1] = temp;

            Count--;
        } // Release


    } // Pool
}
