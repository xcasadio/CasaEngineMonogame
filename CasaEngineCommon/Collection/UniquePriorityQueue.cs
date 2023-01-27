#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace CasaEngineCommon.Collection
{
	/// <summary>
	/// This class represents a priority queue were any given element can only appear once in the queue.
	/// Repeated elements aren´t inserted in the queue.
	/// </summary>
	/// <remarks>
	/// Another homebred implementation. This one is based on the idea of the STL set (don´t know if they implemented
	/// it this way, the lineal search doesn´t seem very effective as it renders the Enqueue operation O(n) instead
	/// of the original O(log n), but I don´t have a better idea at the moment and this one was pretty easy to do).
	/// </remarks>
	/// <typeparam name="T">The type of the elements in the priority queue</typeparam>
	public class UniquePriorityQueue<T> : PriorityQueue<T>
	{
		#region Constructors

		/// <summary>
		/// Default constructor. Uses the default comparer for the elements in the unique priority queue
		/// </summary>
		public UniquePriorityQueue() : base(System.Collections.Generic.Comparer<T>.Default) {}

		/// <summary>
		/// Creates a unique priority queue with a specific IComparer
		/// </summary>
		/// <param name="comparer">The specific IComparer used to compare elements</param>
		public UniquePriorityQueue(IComparer<T> comparer) : base (comparer) {}

		/// <summary>
		/// Creates a unique priority queue with a default capacity and the generic comparer for T
		/// </summary>
		/// <param name="capacity">The initial capacity of the queue</param>
		public UniquePriorityQueue(int capacity) : base(System.Collections.Generic.Comparer<T>.Default, capacity) { }

		/// <summary>
		/// Creates a unique priority queue with a default capacity and a specific comparer for T
		/// </summary>
		/// <param name="comparer">The specific IComparer used to compare elements</param>
		/// <param name="capacity">The initial capacity of the queue</param>
		public UniquePriorityQueue(IComparer<T> comparer, int capacity) : base(comparer, capacity) {}

		#endregion

		#region Methods

		/// <summary>
		/// Enqueues an element in the priority queue if it wasn´t enqueued yet
		/// </summary>
		/// <param name="element">The element we want to add to the priority queue</param>
		/// <returns>The index where the element was enqueued or -1 if the element was repeated</returns>
		public override int Enqueue(T element)
		{
			//Lineal search of the element
			for (int i = 0; i < heapElements.Count; i++)
				if (comparer.Compare(heapElements[i], element) == 0)
					return -1;

			return base.Enqueue(element);
		}

		#endregion
	}
}
