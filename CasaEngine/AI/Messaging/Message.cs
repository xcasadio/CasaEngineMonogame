
namespace CasaEngine.AI.Messaging
{
    [Serializable]
    public class Message
    {

        public const int NoSenderID = -1;



        protected internal int senderID;

        protected internal int recieverID;

        protected internal int type;

        protected internal double dispatchTime;

        protected internal object extraInfo;



        public Message(int senderID, int recieverID, int type, double dispatchTime, object extraInfo)
        {
            String message = String.Empty;

            if (ValidateID(senderID, ref message) == false)
                throw new AIException("senderID", this.GetType().ToString(), message);

            if (ValidateID(recieverID, ref message) == false)
                throw new AIException("recieverID", this.GetType().ToString(), message);

            if (ValidateTime(dispatchTime, ref message) == false)
                throw new AIException("dispatchTime", this.GetType().ToString(), message);

            this.senderID = senderID;
            this.recieverID = recieverID;
            this.type = type;
            this.dispatchTime = dispatchTime;
            this.extraInfo = extraInfo;
        }



        public int SenderID
        {
            get => senderID;
            set
            {
                String message = String.Empty;

                if (ValidateID(value, ref message) == false)
                    throw new AIException("senderID", this.GetType().ToString(), message);

                senderID = value;
            }
        }

        public int RecieverID
        {
            get => recieverID;
            set
            {
                String message = String.Empty;

                if (ValidateID(value, ref message) == false)
                    throw new AIException("recieverID", this.GetType().ToString(), message);

                recieverID = value;
            }
        }

        public int Type
        {
            get => type;
            set => type = value;
        }

        public double DispatchTime
        {
            get => dispatchTime;
            set
            {
                String message = String.Empty;

                if (ValidateTime(value, ref message) == false)
                    throw new AIException("dispatchTime", this.GetType().ToString(), message);

                dispatchTime = value;
            }
        }

        public object ExtraInfo
        {
            get => extraInfo;
            set => extraInfo = value;
        }



        public static bool ValidateID(int id, ref string message)
        {
            if (id < -1)
            {
                message = "ID must  be greater or equal than -1";
                return false;
            }

            return true;
        }

        public static bool ValidateTime(double dispatchTime, ref string message)
        {
            if (dispatchTime < 0)
            {
                message = "You can´t set a negative dispatch time (at least until we design a time travelling machine, should come after Jad)";
                return false;
            }

            return true;
        }

    }
}
