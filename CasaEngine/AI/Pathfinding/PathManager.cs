#region Using Directives

using System;
using System.Collections.Generic;

using CasaEngine.AI.Graphs;
using CasaEngine.AI.Navigation;

#endregion

namespace CasaEngine.AI.Pathfinding
{
	/// <summary>
	/// This class registers PathPlanners to handle time-sliced search requests done to the pathfinding system. It
	/// allocates some time or search cycles every time it´s updated to all searches registered on it
	/// </summary>
	/// <typeparam name="T">
	/// The type of the edges where the search is going to be performed. This parameter is related to the 
	/// PathPlanners that will register in this manager
	/// </typeparam>
	public sealed class PathManager<T>
		where T : WeightedEdge
	{
		#region Delegates

		/// <summary>
		/// This delegate represents the update function the path manager should use
		/// for its searches
		/// </summary>
		internal delegate void UpdateDelegate();

		#endregion

		#region Fields

		/// <summary>
		/// Singleton variable. Uses lazy instantiation
		/// </summary>
		private static readonly PathManager<T> manager = new PathManager<T>();

		/// <summary>
		/// All registered search requests that the path manager holds
		/// </summary>
		internal List<PathPlanner<T>> searchRequests;

		/// <summary>
		/// Number of allocated cycles for processing searches during each update
		/// </summary>
		internal int allocatedCycles;

		/// <summary>
		/// Allocated time in ticks for processing searches during each update
		/// </summary>
		internal long allocatedTime;

		/// <summary>
		/// The update method the path manager uses
		/// </summary>
		internal UpdateDelegate updateMethod;

		#endregion

		#region Constructors

		/// <summary>
		/// Static constructor needed to be thread safe
		/// </summary>
		static PathManager() { }

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <remarks>
		/// By default the manager uses search cycles in the updates and its configured
		/// for nearly infinite cycles per update (all searches are performed till finished)
		/// </remarks>
		private PathManager()
		{
			searchRequests = new List<PathPlanner<T>>();
			
			this.allocatedCycles = int.MaxValue;
			this.allocatedTime = long.MaxValue;

			updateMethod = UpdateWithCycles;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the EntityManager instance
		/// </summary>
		public static PathManager<T> Instance
		{
			get { return manager; }
		}

		/// <summary>
		/// Number of allocated search cycles in each update 
		/// </summary>
		/// <remarks>The manager will use the cycle update function</remarks>
		public int AllocatedCyclesForUpdate
		{
			set
			{
				allocatedCycles = value;
				updateMethod = UpdateWithCycles;
			}
		}

		/// <summary>
		/// Allocated search time in each update 
		/// </summary>
		/// <remarks>The manager will use the time update function</remarks>
		public long AllocatedTimeForUpdate
		{
			set
			{
				allocatedTime = value;
				updateMethod = UpdateWithTime;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Registers a path planner petition for search requests
		/// </summary>
		/// <param name="planner"></param>
		public void Register(PathPlanner<T> planner)
		{
			if (searchRequests.Contains(planner) == true)
				return;

			searchRequests.Add(planner);
		}

		/// <summary>
		/// Unregisters a path planner petition for search requests
		/// </summary>
		/// <param name="planner"></param>
		public void Unregister(PathPlanner<T> planner)
		{
			searchRequests.Remove(planner);
		}

		/// <summary>
		/// Updates the path manager
		/// </summary>
		/// <remarks>
		/// This method updates all search requests in the manager till it runs out
		/// of cycles or time
		/// </remarks>
		public void Update()
		{
			updateMethod();
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// This method uses allocated cycles to update the path manager
		/// </summary>
		private void UpdateWithCycles()
		{
			int elapsedCycles, i;
			SearchState result;

			elapsedCycles = 0;
			i = 0;

			//While there are some cycles left and some search requests
			while (elapsedCycles < allocatedCycles && searchRequests.Count != 0)
			{
				//Do a search cycle
				result = searchRequests[i].CycleOnce();

				//If the search finished, remove it from the path manager
				if (result == SearchState.CompletedAndFound || result == SearchState.CompletedAndNotFound)
				{
					searchRequests.RemoveAt(i);
					i--;
				}

				i++;

				//If the last search request is reached, start again
				if (i == searchRequests.Count)
					i = 0;

				elapsedCycles++;
			}
		}

		/// <summary>
		/// This method uses allocated time to update the path manager
		/// </summary>
		private void UpdateWithTime()
		{
			long elapsedTime, initialTime;
			int i;
			SearchState result;

			initialTime = DateTime.Today.Ticks;
			elapsedTime = 0;
			i = 0;

			//While there is some time left and some search requests
			while (elapsedTime < allocatedTime && searchRequests.Count != 0)
			{
				//Do a search cycle
				result = searchRequests[i].CycleOnce();

				//If the search finished, remove it from the path manager
				if (result == SearchState.CompletedAndFound || result == SearchState.CompletedAndNotFound)
				{
					searchRequests.RemoveAt(i);
					i--;
				}

				i++;

				//If the last search request is reached, start again
				if (i == searchRequests.Count)
					i = 0;

				elapsedTime = DateTime.Today.Ticks - initialTime;
			}
		}

		#endregion
	}
}
