namespace CasaEngine.AI.Graphs
{
    [Serializable]
    public class WeightedEdge : Edge
    {
        protected internal float cost = 0.0f;

        public WeightedEdge()
        { }

        public WeightedEdge(int start, int end, float cost) : base(start, end)
        {
            string message = string.Empty;

            if (ValidateCost(cost, ref message) == false)
            {
                throw new AiException("cost", GetType().ToString(), message);
            }

            this.cost = cost;
        }

        public float Cost
        {
            get => cost;
            set
            {
                string message = string.Empty;

                if (ValidateCost(value, ref message) == false)
                {
                    throw new AiException("cost", GetType().ToString(), message);
                }

                cost = value;
            }
        }

        public static bool ValidateCost(float cost, ref string message)
        {
            if (cost < 0)
            {
                message = "The cost must be equal or greater than 0";
                return false;
            }

            return true;
        }

    }
}
