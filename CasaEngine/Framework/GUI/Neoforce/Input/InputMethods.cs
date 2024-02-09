namespace TomShane.Neoforce.Controls.Input;

[Flags]
public enum InputMethods
{
    None = 0x00,
    Keyboard = 0x01,
    Mouse = 0x02,
    GamePad = 0x04,
    All = Keyboard | Mouse | 0x04
}