namespace CasaEngine.Framework.AI.Graphs
{
    [Serializable]
    public class WeightedEdge : Edge
    {
        protected internal float cost;

        public WeightedEdge()
        { }

        public WeightedEdge(int start, int end, float cost) : base(start, end)
        {
            var message = string.Empty;

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
                var message = string.Empty;

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
