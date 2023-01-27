#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace CasaEngineCommon.Collection
{
	/// <summary>
	/// This interface defines the needed operations to create a PriorityQueue
	/// </summary>
	/// <typeparam name="T">The type of the elements in the priority queue</typeparam>
	public interface IPriorityQueue<T> : ICollection<T>, IList<T>, ICloneable
	{
		#region Methods

		/// <summary>
		/// Push an object onto the PQ
		/// </summary>
		/// <param name="element">The new object</param>
		/// <returns>The index in the list where the object is _now_. This will change when objects are taken from or put onto the PQ</returns>
		int Enqueue(T element);

		/// <summary>
		/// Get the smallest object and remove it
		/// </summary>
		/// <returns>The smallest object</returns>
		T Dequeue();

		/// <summary>
		/// Get the smallest object without removing it
		/// </summary>
		/// <returns>The smallest object</returns>
		T Peek();

		#endregion
	}
}
