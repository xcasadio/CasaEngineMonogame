using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;

namespace CasaEngine.Framework.UI;

/// <summary>
/// What a <see cref="DataDrivenScreen"/> asks of the game to check and run its actions (ADR-0078). These are
/// delegates so that a test can stand in for the game: a <see cref="UIRoot"/> cannot exist without a graphics device.
/// </summary>
public sealed class UIScreenActionContext
{
    /// <summary>True when the name or id designates a screen asset of the project.</summary>
    public required Func<string, bool> ScreenExists { get; init; }

    /// <summary>True when the file name designates a catalogued world.</summary>
    public required Func<string, bool> WorldExists { get; init; }

    /// <summary>Takes a hold on the sound asset the name or id designates. Throws when there is none.</summary>
    public required Func<string, AssetHandle<SoundAsset>> AcquireSound { get; init; }

    /// <summary>True when the screen is at the top of its stack.</summary>
    public required Func<IUIScreen, bool> IsTopScreen { get; init; }

    /// <summary>Opens the screen the name or id designates, on top of the others.</summary>
    public required Action<string> OpenScreen { get; init; }

    /// <summary>Closes the screen.</summary>
    public required Action<IUIScreen> CloseScreen { get; init; }

    /// <summary>Asks for the world the file name designates.</summary>
    public required Action<string> ChangeWorld { get; init; }

    /// <summary>Plays the sound.</summary>
    public required Action<SoundAsset> PlaySound { get; init; }

    /// <summary>Quits the game.</summary>
    public required Action Quit { get; init; }

    /// <summary>
    /// The resolution the screen's XAML is written at, when the project scales its screens to it (ADR-0079), or null
    /// to leave the window as the XAML declares it.
    /// </summary>
    public Point? DesignResolution { get; init; }
}
