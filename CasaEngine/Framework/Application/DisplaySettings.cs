namespace CasaEngine.Framework.Application;

public readonly struct DisplaySettings
{
    public int Width { get; }

    public int Height { get; }

    public bool IsFullScreen { get; }

    public bool IsVSyncEnabled { get; }

    public DisplaySettings(int width, int height, bool isFullScreen, bool isVSyncEnabled)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
        IsFullScreen = isFullScreen;
        IsVSyncEnabled = isVSyncEnabled;
    }
}