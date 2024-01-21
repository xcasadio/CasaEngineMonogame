namespace CasaEngine.Framework.AI.Messaging;

public interface IMessageManager
{

    void ResetManager(double precision);

    void SendMessage(Guid senderId, Guid receiverId, double delayTime, int type, object extraInfo);

    void Update();

}