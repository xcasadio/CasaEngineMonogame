using CasaEngine.AI.Graphs;
using Microsoft.Xna.Framework;


namespace CasaEngine.AI.Navigation
{
    [Serializable]
    public class NavigationEdge
    {

        protected internal Vector3 Start;

        protected internal Vector3 End;

        protected internal EdgeInformation Information;



        public NavigationEdge(Vector3 start, Vector3 end, EdgeInformation information)
        {
            this.start = start;
            this.end = end;
            this.information = information;
        }



        public Vector3 Start
        {
            get => start;
            set => start = value;
        }

        public Vector3 End
        {
            get => end;
            set => end = value;
        }

        public EdgeInformation Information
        {
            get => information;
            set => information = value;
        }

    }
}
