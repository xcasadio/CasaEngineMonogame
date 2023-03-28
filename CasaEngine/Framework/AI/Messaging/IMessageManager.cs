namespace CasaEngine.Framework.AI.Messaging;

public interface IMessageManager
{

    void ResetManager(double precision);

    void SendMessage(int senderId, int recieverId, double delayTime, int type, object extraInfo);

    void Update();

}