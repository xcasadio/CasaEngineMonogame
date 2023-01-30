
namespace CasaEngine.AI.Messaging
{
    public interface IMessageManager
    {

        void ResetManager(double precision);

        void SendMessage(int senderID, int recieverID, double delayTime, int type, object extraInfo);

        void Update();

    }
}
