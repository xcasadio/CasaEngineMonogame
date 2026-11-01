using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering.ScreenEffects;

namespace CasaEngine.Framework.Application;

/// <summary>
/// What the context of a data-driven screen takes from the game. A type of its own because a test has no game: it
/// passes these to <see cref="GameManager.CreateScreenActionContext"/>, so only the reads of the game stay out of reach.
/// </summary>
/// <param name="ScreenEffects">The fade service. A function because the context outlives the moment it is built.</param>
/// <param name="VirtualResolution">The active virtual resolution, null when there is none.</param>
/// <param name="HoldFont">Takes a hold on the bitmap font of that catalogue id.</param>
/// <param name="HasUIInputMapping">True when the project names a UI input mapping.</param>
/// <param name="Quit">Quits the game.</param>
internal sealed record ScreenGameServices(
    Func<ScreenEffectService> ScreenEffects,
    VirtualResolutionSettings VirtualResolution,
    Func<Guid, IDisposable> HoldFont,
    bool HasUIInputMapping,
    Action Quit);
