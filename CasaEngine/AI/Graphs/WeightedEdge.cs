
using System;




namespace CasaEngine.AI.Graphs
{
    /// <summary>
    /// This class represents an edge with a cost associated to it
    /// </summary>
    [Serializable]
    public class WeightedEdge : Edge
    {

        /// <summary>
        /// Cost of the edge
        /// </summary>
        protected internal float cost = 0.0f;



        /// <summary>
        /// Default constructor
        /// </summary>
        public WeightedEdge()
        { }

        /// <summary>
        /// Constructor that creates an edge with start, end and cost
        /// </summary>
        /// <param name="start">Start node of the edge</param>
        /// <param name="end">End node of the edge</param>
        /// <param name="cost">Cost of the edge</param>
        public WeightedEdge(int start, int end, float cost) : base(start, end)
        {
            String message = String.Empty;

            if (ValidateCost(cost, ref message) == false)
                throw new AIException("cost", this.GetType().ToString(), message);

            this.cost = cost;
        }



        /// <summary>
        /// Gets or sets the edge cost
        /// </summary>
        public float Cost
        {
            get { return cost; }
            set
            {
                String message = String.Empty;

                if (ValidateCost(value, ref message) == false)
                    throw new AIException("cost", this.GetType().ToString(), message);

                cost = value;
            }
        }



        /// <summary>
        /// Validates if the edge cost value is correct (>= 0)
        /// </summary>
        /// <param name="cost">The edge cost we want to validate</param>
        /// <param name="message">Message explaining why the validation failed</param>
        /// <returns>True if the value is correct. False if it is not</returns>
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
