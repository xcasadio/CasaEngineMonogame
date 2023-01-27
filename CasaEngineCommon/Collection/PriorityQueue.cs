#region Using Directives

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

#endregion

namespace CasaEngineCommon.Collection
{
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
		#region Fields

		/// <summary>
		/// The elements in the heap
		/// </summary>
		protected List<T> heapElements = new List<T>();

		/// <summary>
		/// Used to compare elements when reordering the heap
		/// </summary>
		protected IComparer<T> comparer;

		#endregion

		#region Contructors

		/// <summary>
		/// Default constructor. Uses the default comparer for the elements in the priority queue
		/// </summary>
		public PriorityQueue() : this(System.Collections.Generic.Comparer<T>.Default) {}

		/// <summary>
		/// Creates a priority queue with a specific IComparer
		/// </summary>
		/// <param name="comparer">The specific IComparer used used to compare elements</param>
		public PriorityQueue(IComparer<T> comparer)
		{
			this.comparer = comparer;
		}

		/// <summary>
		/// Creates a priority queue with a default capacity and the generic comparer for T
		/// </summary>
		/// <param name="capacity">The initial capacity of the queue</param>
		public PriorityQueue(int capacity) : this(System.Collections.Generic.Comparer<T>.Default, capacity) { }

		/// <summary>
		/// Creates a priority queue with a default capacity and a specific comparer for T
		/// </summary>
		/// <param name="comparer">The specific IComparer used to compare elements</param>
		/// <param name="capacity">The initial capacity of the queue</param>
		public PriorityQueue(IComparer<T> comparer, int capacity)
		{
			this.comparer = comparer;
			heapElements.Capacity = capacity;
		}

		#endregion

		#region IPriorityQueue<T> Members

		/// <summary>
		/// Push an object onto the PQ
		/// </summary>
		/// <param name="element">The new object</param>
		/// <returns>The index in the list where the object is _now_. This will change when objects are taken from or put onto the PQ</returns>
		public virtual int Enqueue(T element)
		{
			int p, p2;

			p = heapElements.Count;
			heapElements.Add(element);

			//Heapify up
			do
			{
				if(p==0)
					break;
				
				p2 = (p-1)/2;

				if(Compare(p, p2)<0)
				{
					Swap(p, p2);
					p = p2;
				}
				
				else
					break;
			} while(true);

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

			if (heapElements.Count == 0)
				return default(T);

			//Get the smallest element
			result = heapElements[0];

			//Heapify down
			p = 0;
			heapElements[0] = heapElements[heapElements.Count-1];
			heapElements.RemoveAt(heapElements.Count-1);
			
			do
			{
				pn = p;
				p1 = 2*p+1;
				p2 = 2*p+2;

				if(heapElements.Count>p1 && Compare(p, p1)>0)
					p = p1;

				if(heapElements.Count>p2 && Compare(p, p2)>0)
					p = p2;
				
				if(p==pn)
					break;

				Swap(p, pn);
			}while(true);

			return result;
		}

		/// <summary>
		/// Get the smallest object without removing it
		/// </summary>
		/// <returns>The smallest object</returns>
		public T Peek()
		{
			if(heapElements.Count > 0)
				return heapElements[0];

			return default(T);
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Swaps two elements
		/// </summary>
		/// <param name="i">The index of the first element to swap</param>
		/// <param name="j">The index of the second element to swap</param>
		protected virtual void Swap(int i, int j)
		{
			T h;

			h = heapElements[i];
			heapElements[i] = heapElements[j];
			heapElements[j] = h;
		}

		/// <summary>
		/// Compares two elements
		/// </summary>
		/// <param name="i">The index of the first element to compare</param>
		/// <param name="j">The index of the first element to compare</param>
		/// <returns>The result of the compare method</returns>
		protected virtual int Compare(int i, int j)
		{
			return comparer.Compare(heapElements[i], heapElements[j]);
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
					break;

				p2 = (p - 1) / 2;

				if (Compare(p, p2) < 0)
				{
					Swap(p, p2);
					p = p2;
				}

				else
					break;
			} while (true);

			if (p < i)
				return;

			//Heapify down
			do
			{
				pn = p;
				p1 = 2 * p + 1;
				p2 = 2 * p + 2;

				if (heapElements.Count > p1 && Compare(p, p1) > 0)
					p = p1;

				if (heapElements.Count > p2 && Compare(p, p2) > 0)
					p = p2;

				if (p == pn)
					break;

				Swap(p, pn);
			} while (true);
		}

		#endregion

		#region IEnumerable<T> Members

		/// <summary>
		/// Returns an enumerator that iterates the priority queue
		/// </summary>
		/// <returns>The enumerator used to iterate the priority queue</returns>
		public IEnumerator<T> GetEnumerator()
		{
			return heapElements.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		/// <summary>
		/// Not implemented
		/// </summary>
		/// <returns></returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotSupportedException("You can´t iterate through the elements of a priority queue using a non-generic enumerator");
		}

		#endregion

		#region ICollection<T> Members

		/// <summary>
		/// Number of elements in the heap
		/// </summary>
		public int Count
		{
			get
			{
				return heapElements.Count;
			}
		}

		/// <summary>
		/// Indicates if the collection is readonly. Always returns false
		/// </summary>
		public bool IsReadOnly
		{
			get
			{
				return false;
			}
		}

		/// <summary>
		/// Indicates if the PQ contais an element
		/// </summary>
		/// <param name="value">Value we want to test</param>
		/// <returns>True if the value was in the PQ, false otherwise</returns>
		public bool Contains(T value)
		{
			return heapElements.Contains(value);
		}

		/// <summary>
		/// Removes all elements from the heap
		/// </summary>
		public void Clear()
		{
			heapElements.Clear();
		}

		/// <summary>
		/// Copies the elements of the heap into an array
		/// </summary>
		/// <param name="array">The source array where we are going to copy the heap elements</param>
		/// <param name="arrayIndex">The index to start copying the elements</param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			heapElements.CopyTo(array, arrayIndex);
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

		#endregion

		#region IList<T> Members

		/// <summary>
		/// Searches for the element in the heap
		/// </summary>
		/// <param name="item">The element we want to search</param>
		/// <returns>The index of the element</returns>
		public int IndexOf(T item)
		{
			return heapElements.IndexOf(item);
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
			get
			{
				return heapElements[index];
			}

			set
			{
				heapElements[index] = value;
				Update(index);
			}
		}

		#endregion

		#region ICloneable Members

		/// <summary>
		/// Creates a clone from this object
		/// </summary>
		/// <returns>The clone of the object</returns>
		public object Clone()
		{
			MemoryStream memory;
			BinaryFormatter formater;
			object clone;

			using (memory = new MemoryStream())
			{
				//Serialize ourselves
				formater = new BinaryFormatter();
				formater.Serialize(memory, this);

				//Move the memory buffer to the start
				memory.Seek(0, SeekOrigin.Begin);

				//Undo the serialization in the new clone object
				clone = formater.Deserialize(memory);

				return clone;
			}
		}

		#endregion
	}
}
