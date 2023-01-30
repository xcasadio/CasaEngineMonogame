using CasaEngine.AI.Graphs;


namespace CasaEngine.AI.Pathfinding
{
    public sealed class PathManager<T>
        where T : WeightedEdge
    {

        internal delegate void UpdateDelegate();



        private static readonly PathManager<T> manager = new PathManager<T>();

        internal List<PathPlanner<T>> searchRequests;

        internal int allocatedCycles;

        internal long allocatedTime;

        internal UpdateDelegate updateMethod;



        static PathManager() { }

        private PathManager()
        {
            searchRequests = new List<PathPlanner<T>>();

            this.allocatedCycles = int.MaxValue;
            this.allocatedTime = long.MaxValue;

            updateMethod = UpdateWithCycles;
        }



        public static PathManager<T> Instance => manager;

        public int AllocatedCyclesForUpdate
        {
            set
            {
                allocatedCycles = value;
                updateMethod = UpdateWithCycles;
            }
        }

        public long AllocatedTimeForUpdate
        {
            set
            {
                allocatedTime = value;
                updateMethod = UpdateWithTime;
            }
        }



        public void Register(PathPlanner<T> planner)
        {
            if (searchRequests.Contains(planner) == true)
                return;

            searchRequests.Add(planner);
        }

        public void Unregister(PathPlanner<T> planner)
        {
            searchRequests.Remove(planner);
        }

        public void Update()
        {
            updateMethod();
        }



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

    }
}
