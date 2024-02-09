namespace TomShane.Neoforce.Controls;

[Flags]
public enum ConsoleMessageFormats
{
    None = 0x00,
    ChannelName = 0x01,
    TimeStamp = 0x02,
    Sender = 0x03,
    All = Sender | ChannelName | TimeStamp
}