namespace TomShane.Neoforce.Controls;

public class ChannelList : EventedList<ConsoleChannel>
{

    public ConsoleChannel this[string name]
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                var s = this[i];
                if (string.Equals(s.Name, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    return s;
                }
            }
            return default;
        }

        set
        {
            for (var i = 0; i < Count; i++)
            {
                var s = this[i];
                if (string.Equals(s.Name, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    this[i] = value;
                }
            }
        }
    }

    public ConsoleChannel this[byte index]
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                var s = this[i];
                if (s.Index == index)
                {
                    return s;
                }
            }
            return default;
        }

        set
        {
            for (var i = 0; i < Count; i++)
            {
                var s = this[i];
                if (s.Index == index)
                {
                    this[i] = value;
                }
            }
        }
    }

}