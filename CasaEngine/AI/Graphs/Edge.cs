using System.Runtime.Serialization.Formatters.Binary;





namespace CasaEngine.AI.Graphs
{
    [Serializable]
    public class Edge : ICloneable
    {

        public const int InvalidNode = -1;



        protected internal int Start;

        protected internal int End;



        public Edge()
        {
            start = Edge.InvalidNode;
            end = Edge.InvalidNode;
        }

        public Edge(int start, int end)
        {
            String message = String.Empty;

            if (ValidateNode(start, ref message) == false)
                throw new AiException("start", this.GetType().ToString(), message);

            if (ValidateNode(end, ref message) == false)
                throw new AiException("end", this.GetType().ToString(), message);

            this.start = start;
            this.end = end;
        }



        public int Start
        {
            get => start;
            set
            {
                String message = String.Empty;

                if (ValidateNode(value, ref message) == false)
                    throw new AiException("start", this.GetType().ToString(), message);

                this.start = value;
            }
        }

        public int End
        {
            get => end;
            set
            {

                String message = String.Empty;

                if (ValidateNode(value, ref message) == false)
                    throw new AiException("end", this.GetType().ToString(), message);

                this.end = value;
            }
        }



        public static bool ValidateNode(int index, ref string message)
        {
            if (index < Edge.InvalidNode)
            {
                message = "The node index number must be equal or greater than " + Edge.InvalidNode;
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

    }
}
