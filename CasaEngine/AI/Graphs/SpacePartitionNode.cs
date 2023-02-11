using Microsoft.Xna.Framework;


namespace CasaEngine.AI.Graphs
{
    [Serializable]
    public class SpacePartitionNode : NavigationNode
    {

        protected internal int SpaceSector;



        public SpacePartitionNode() : base()
        { }

        public SpacePartitionNode(Vector3 position, int spaceSector)
            : base(position)
        {
            this.spaceSector = spaceSector;
        }



        public int SpaceSector => spaceSector;


        protected internal override bool IsNeighbour(int spacePartitionSector, Vector3 searchPosition, float searchRange)
        {
            //Only search if the node and the search position are in the same space sector
            if (this.spaceSector == spacePartitionSector)
                return base.IsNeighbour(spacePartitionSector, searchPosition, searchRange);

            return false;
        }

    }
}
