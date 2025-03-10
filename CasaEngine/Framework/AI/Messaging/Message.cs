namespace CasaEngine.Framework.AI.Messaging;

[Serializable]
public class Message
{
    public const int NoSenderId = -1;

    protected Guid _senderId;
    protected Guid _recieverId;
    protected int _type;
    protected double _dispatchTime;
    protected object _extraInfo;

    public Message(Guid senderId, Guid recieverId, int type, double dispatchTime, object extraInfo)
    {
        var message = string.Empty;

        if (ValidateId(senderId, ref message) == false)
        {
            throw new AiException("senderID", GetType().ToString(), message);
        }

        if (ValidateId(recieverId, ref message) == false)
        {
            throw new AiException("recieverID", GetType().ToString(), message);
        }

        if (ValidateTime(dispatchTime, ref message) == false)
        {
            throw new AiException("dispatchTime", GetType().ToString(), message);
        }

        _senderId = senderId;
        _recieverId = recieverId;
        _type = type;
        _dispatchTime = dispatchTime;
        _extraInfo = extraInfo;
    }

    public Guid SenderID
    {
        get => _senderId;
        set
        {
            var message = string.Empty;

            if (ValidateId(value, ref message) == false)
            {
                throw new AiException("senderID", GetType().ToString(), message);
            }

            _senderId = value;
        }
    }

    public Guid RecieverID
    {
        get => _recieverId;
        set
        {
            var message = string.Empty;

            if (ValidateId(value, ref message) == false)
            {
                throw new AiException("recieverID", GetType().ToString(), message);
            }

            _recieverId = value;
        }
    }

    public int Type
    {
        get => _type;
        set => _type = value;
    }

    public double DispatchTime
    {
        get => _dispatchTime;
        set
        {
            var message = string.Empty;

            if (ValidateTime(value, ref message) == false)
            {
                throw new AiException("dispatchTime", GetType().ToString(), message);
            }

            _dispatchTime = value;
        }
    }

    public object ExtraInfo
    {
        get => _extraInfo;
        set => _extraInfo = value;
    }



    public static bool ValidateId(Guid id, ref string message)
    {
        if (id == Guid.Empty)
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
            message = "You can�t set a negative dispatch time (at least until we design a time travelling machine, should come after Jad)";
            return false;
        }

        return true;
    }

}