#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace CasaEngineCommon.Collection
{
	/// <summary>
	/// This class represents an indexed priority queue. Instead of ordering the elements themselves, the priority queue
	/// holds indexes to a list that contains the true elements to order
	/// </summary>
	/// <remarks>
	/// My own home-implementation. Seems to work, but I haven´t checked in an algorithm book other ways to implement an
	/// indexed priority queue using a two-way heap (or a d-way heap)
	/// </remarks>
	/// <typeparam name="T">The type of the indexed elements</typeparam>
	public class IndexedPriorityQueue<T> : PriorityQueue<int>
	{
		#region Fields

		/// <summary>
		/// The elements indexed
		/// </summary>
		protected List<T> indexedElements = new List<T>();

		/// <summary>
		/// The comparer for the indexes
		/// </summary>
		protected IComparer<T> indexComparer;

		/// <summary>
		/// This list gives us the index where an element from the indexedPriority list is in the heapElements list. It allows
		/// to move through the priority queue in the 2 ways (from index to indexed element and viceversa)
		/// </summary>
		protected List<int> reversedIndexes;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor. Uses the default comparer for the elements in the indexed priority queue
		/// </summary>
		public IndexedPriorityQueue() : this(System.Collections.Generic.Comparer<T>.Default) {}

		/// <summary>
		/// Creates an indexed priority queue with a specific IComparer
		/// </summary>
		/// <param name="indexComparer">The specific IComparer used to compare the indexed elements</param>
		public IndexedPriorityQueue(IComparer<T> indexComparer)
		{
			this.indexComparer = indexComparer;
		}

		/// <summary>
		/// Creates an indexed priority queue with a generic comparer and with the indexed elements list
		/// </summary>
		/// <param name="indexedElements">The list where we are going to index the priority queue</param>
		public IndexedPriorityQueue(List<T> indexedElements) : this(System.Collections.Generic.Comparer<T>.Default)
		{
			this.indexedElements = indexedElements;
			
			//Create and initialize the reversed indexes list
			reversedIndexes = new List<int>(indexedElements.Count);
			for (int i = 0; i < indexedElements.Count; i++)
				reversedIndexes.Add(-1);

			this.heapElements.Capacity = indexedElements.Count;
		}

		/// <summary>
		/// Creates an indexed priority queue with a specific comparer and with the indexed elements list
		/// </summary>
		/// <param name="indexComparer">The specific IComparer used to compare the indexed elements</param>
		/// <param name="indexedElements">The list where we are going to index the priority queue</param>
		public IndexedPriorityQueue(IComparer<T> indexComparer, List<T> indexedElements) : this(indexedElements)
		{
			this.indexComparer = indexComparer;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the indexed elements list
		/// </summary>
		public List<T> IndexedElements
		{
			get { return indexedElements; }
			set
			{
				indexedElements = value;
				reversedIndexes = new List<int>(indexedElements.Count);

				for (int i = 0; i < indexedElements.Count; i++)
					reversedIndexes.Add(-1);
			}
		}

		#endregion

		#region IPriorityQueue<T> Members

		/// <summary>
		/// Push an object onto the PQ
		/// </summary>
		/// <param name="element">The new object</param>
		/// <returns>The index in the list where the object is _now_. This will change when objects are taken from or put onto the PQ</returns>
		public override int Enqueue(int element)
		{
			int p, p2;

			p = heapElements.Count;
			heapElements.Add(element);
			reversedIndexes[element] = p;

			//Heapify up
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

			return p;
		}

		/// <summary>
		/// Get the smallest object and remove it
		/// </summary>
		/// <returns>The smallest object</returns>
		public override int Dequeue()
		{
			int result;
			int p, p1, p2, pn;

			//Get the smallest element
			result = heapElements[0];
			reversedIndexes[result] = -1;

			//Heapify down
			p = 0;
			heapElements[0] = heapElements[heapElements.Count - 1];
			heapElements.RemoveAt(heapElements.Count - 1);

			if (heapElements.Count != 0)
				reversedIndexes[heapElements[0]] = 0;

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

			return result;
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Swaps two elements and the indexed elements
		/// </summary>
		/// <param name="i">The first index to swap</param>
		/// <param name="j">The second index to swap</param>
		protected override void Swap(int i, int j)
		{
			int h;

			h = reversedIndexes[heapElements[i]];
			reversedIndexes[heapElements[i]] = reversedIndexes[heapElements[j]];
			reversedIndexes[heapElements[j]] = h;

			base.Swap(i, j);
		}

		/// <summary>
		/// Compares two indexed elements
		/// </summary>
		/// <param name="i">The first index to compare</param>
		/// <param name="j">The second index compare</param>
		protected override int Compare(int i, int j)
		{
			return indexComparer.Compare(indexedElements[heapElements[i]], indexedElements[heapElements[j]]);
		}

		#endregion

		#region Methods

		/// <summary>
		/// Indicates the indexed priority queue that we have changed the value of the indexed element i,
		/// and that it should update the heap
		/// </summary>
		/// <param name="i">The element we have updated</param>
		public void ChangePriority(int i)
		{
			this.Update(reversedIndexes[i]);
		}

		#endregion
	}
}
