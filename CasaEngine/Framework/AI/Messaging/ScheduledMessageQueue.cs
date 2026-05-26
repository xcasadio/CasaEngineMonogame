namespace CasaEngine.Framework.AI.Messaging;

internal sealed class ScheduledMessageQueue
{
    private readonly MessageComparer _duplicateComparer;
    private readonly PriorityQueue<Message, ScheduledMessagePriority> _messages = new();
    private long _nextSequence;

    public ScheduledMessageQueue(double duplicatePrecision)
    {
        _duplicateComparer = new MessageComparer(duplicatePrecision);
    }

    public int Count => _messages.Count;

    public bool Enqueue(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        foreach ((Message queuedMessage, ScheduledMessagePriority _) in _messages.UnorderedItems)
        {
            if (_duplicateComparer.Compare(queuedMessage, message) == 0)
            {
                return false;
            }
        }

        _messages.Enqueue(message, new ScheduledMessagePriority(message.DispatchTime, _nextSequence++));
        return true;
    }

    public Message Peek()
    {
        return _messages.Peek();
    }

    public Message Dequeue()
    {
        return _messages.Dequeue();
    }

    public void Clear()
    {
        _messages.Clear();
        _nextSequence = 0;
    }

    private readonly struct ScheduledMessagePriority : IComparable<ScheduledMessagePriority>
    {
        private readonly double _dispatchTime;
        private readonly long _sequence;

        public ScheduledMessagePriority(double dispatchTime, long sequence)
        {
            _dispatchTime = dispatchTime;
            _sequence = sequence;
        }

        public int CompareTo(ScheduledMessagePriority other)
        {
            int timeComparison = _dispatchTime.CompareTo(other._dispatchTime);
            if (timeComparison != 0)
            {
                return timeComparison;
            }

            return _sequence.CompareTo(other._sequence);
        }
    }
}