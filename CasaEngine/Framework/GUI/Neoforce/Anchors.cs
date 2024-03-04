namespace CasaEngine.Framework.GUI.Neoforce;

[Flags]
public enum Anchors
{
    None = 0x00,
    Left = 0x01,
    Top = 0x02,
    Right = 0x04,
    Bottom = 0x08,
    Horizontal = Left | Right,
    Vertical = Top | Bottom,
    All = Left | Top | Right | Bottom
}