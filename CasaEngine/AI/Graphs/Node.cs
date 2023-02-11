using System.Runtime.Serialization.Formatters.Binary;




using Microsoft.Xna.Framework;



namespace CasaEngine.AI.Graphs
{
    [Serializable]
    public class Node : ICloneable
    {

        public const int NoParent = -1;



        protected internal int Index;



        public Node()
        {
            index = Edge.InvalidNode;
        }

        public Node(int index)
        {
            String message = String.Empty;

            if (ValidateIndex(index, ref message) == false)
                throw new AiException("index", this.GetType().ToString(), message);

            this.index = index;
        }



        public int Index
        {
            get => index;
            protected internal set => this.index = value;
        }



        public static bool ValidateIndex(int index, ref string message)
        {
            if (index < Edge.InvalidNode)
            {
                message = "The index value of the node can't be less than " + Edge.InvalidNode;
                return false;
            }

            return true;
        }



        public object Clone()
        {
            MemoryStream memory;
            BinaryFormatter formater;
            object clone;

            using (memory = new MemoryStream())
            {
                //Serialize ourselves
                formater = new BinaryFormatter();
                formater.Serialize(memory, this);

                //Move the memory buffer to the start
                memory.Seek(0, SeekOrigin.Begin);

                //Undo the serialization in the new clone object
                clone = formater.Deserialize(memory);

                return clone;
            }
        }



        protected internal virtual bool IsNeighbour(int spacePartitionSector, Vector3 searchPosition, float searchRange)
        {
            return true;
        }

    }
}
