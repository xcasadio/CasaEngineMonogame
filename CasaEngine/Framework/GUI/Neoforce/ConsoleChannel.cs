namespace CasaEngine.Framework.GUI.Neoforce;

public class ConsoleChannel
{
    private string _name;
    private byte _index;
    private Color _color;

    public ConsoleChannel(byte index, string name, Color color)
    {
        _name = name;
        _index = index;
        _color = color;
    }

    public virtual byte Index
    {
        get => _index;
        set => _index = value;
    }

    public virtual Color Color
    {
        get => _color;
        set => _color = value;
    }

    public virtual string Name
    {
        get => _name;
        set => _name = value;
    }
}