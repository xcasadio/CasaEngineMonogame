namespace CasaEngine.Framework.GUI.Neoforce;

public struct ConsoleMessage
{
    public readonly string Text;
    public readonly byte Channel;
    public DateTime Time;
    public readonly string Sender;

    public ConsoleMessage(string sender, string text, byte channel)
    {
        Text = text;
        Channel = channel;
        Time = DateTime.Now;
        Sender = sender;
    }
}