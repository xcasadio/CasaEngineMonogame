using CasaEngine.AI.Graphs;


namespace CasaEngine.AI.Pathfinding
{
    public sealed class PathManager<T>
        where T : WeightedEdge
    {

        internal delegate void UpdateDelegate();



        private static readonly PathManager<T> Manager = new PathManager<T>();

        internal List<PathPlanner<T>> SearchRequests;

        internal int AllocatedCycles;

        internal long AllocatedTime;

        internal UpdateDelegate UpdateMethod;



        static PathManager() { }

        private PathManager()
        {
            SearchRequests = new List<PathPlanner<T>>();

            this.AllocatedCycles = int.MaxValue;
            this.AllocatedTime = long.MaxValue;

            UpdateMethod = UpdateWithCycles;
        }



        public static PathManager<T> Instance => Manager;

        public int AllocatedCyclesForUpdate
        {
            set
            {
                AllocatedCycles = value;
                UpdateMethod = UpdateWithCycles;
            }
        }

        public long AllocatedTimeForUpdate
        {
            set
            {
                AllocatedTime = value;
                UpdateMethod = UpdateWithTime;
            }
        }



        public void Register(PathPlanner<T> planner)
        {
            if (SearchRequests.Contains(planner) == true)
                return;

            SearchRequests.Add(planner);
        }

        public void Unregister(PathPlanner<T> planner)
        {
            SearchRequests.Remove(planner);
        }

        public void Update()
        {
            UpdateMethod();
        }



        private void UpdateWithCycles()
        {
            int elapsedCycles, i;
            SearchState result;

            elapsedCycles = 0;
            i = 0;

            //While there are some cycles left and some search requests
            while (elapsedCycles < AllocatedCycles && SearchRequests.Count != 0)
            {
                //Do a search cycle
                result = SearchRequests[i].CycleOnce();

                //If the search finished, remove it from the path manager
                if (result == SearchState.CompletedAndFound || result == SearchState.CompletedAndNotFound)
                {
                    SearchRequests.RemoveAt(i);
                    i--;
                }

                i++;

                //If the last search request is reached, start again
                if (i == SearchRequests.Count)
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
            while (elapsedTime < AllocatedTime && SearchRequests.Count != 0)
            {
                //Do a search cycle
                result = SearchRequests[i].CycleOnce();

                //If the search finished, remove it from the path manager
                if (result == SearchState.CompletedAndFound || result == SearchState.CompletedAndNotFound)
                {
                    SearchRequests.RemoveAt(i);
                    i--;
                }

                i++;

                //If the last search request is reached, start again
                if (i == SearchRequests.Count)
                    i = 0;

                elapsedTime = DateTime.Today.Ticks - initialTime;
            }
        }

    }
}
