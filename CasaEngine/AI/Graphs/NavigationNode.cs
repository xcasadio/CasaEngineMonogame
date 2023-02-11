using Microsoft.Xna.Framework;


namespace CasaEngine.AI.Graphs
{
    [Serializable]
    public class NavigationNode : Node
    {

        protected internal Vector3 Position;



        public NavigationNode() : base()
        { }

        public NavigationNode(Vector3 position) : base()
        {
            this.position = position;
        }



        public Vector3 Position
        {
            get => position;
            set => position = value;
        }



        protected internal override bool IsNeighbour(int spacePartitionSector, Vector3 searchPosition, float searchRange)
        {

            //float parameter;

            //If the node is too far from the search position return false
            //float dist = (this.position - searchPosition).LengthSq();
            //if ((this.position - searchPosition).LengthSq() > searchRange)
            //    return false;

            return true;

            //return !Jad.Physics.WorldRayCast(ref this.position, ref searchPosition, out parameter);
        }

    }
}
