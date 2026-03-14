namespace CasaEngine.Framework.GUI;

public readonly record struct UIViewInputState(
    bool IsPointerOverUI,
    bool IsKeyboardCaptured,
    bool HasModalInput)
{
    public static UIViewInputState Empty { get; } = new(false, false, false);
}