
namespace CasaEngine.AI.Graphs
{
    [Serializable]
    public class AnnotatedEdge : WeightedEdge
    {

        protected internal EdgeInformation information;

        public AnnotatedEdge()
        {
            information = EdgeInformation.Normal;
        }

        public AnnotatedEdge(int start, int end, float cost, EdgeInformation information)
            : base(start, end, cost)
        {
            this.information = information;
        }

        public EdgeInformation Information
        {
            get => information;
            set => information = value;
        }

    }
}
