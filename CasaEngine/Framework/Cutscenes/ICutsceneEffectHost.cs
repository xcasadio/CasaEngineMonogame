using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Rendering.ScreenEffects;

namespace CasaEngine.Framework.Cutscenes;

/// <summary>
/// Lets whoever drives a <see cref="CutsceneDirector"/> supply the game-wide effects of a play (sound, screen fades) and
/// refuse the requests a play addresses to the running game. With no host set, a play uses the services of the world's
/// game and its requests reach the game, as usual.
/// </summary>
public interface ICutsceneEffectHost
{
    /// <summary>
    /// The audio service the sound and music actions use while this host is set. Null plays nothing and never falls
    /// back on the game's service.
    /// </summary>
    AudioService Audio { get; }

    /// <summary>
    /// The screen effect service the fade actions use while this host is set. Null makes the fades end at once and
    /// never falls back on the game's service.
    /// </summary>
    ScreenEffectService ScreenEffects { get; }

    /// <summary>
    /// Asked before an action that addresses the running game: <c>ChangeWorld</c>, <c>OpenScreen</c>, <c>PlayVideo</c> and
    /// <c>ExitOnInput</c>. Returns false to skip the action: nothing is asked of the game and the play carries on
    /// with the next action.
    /// </summary>
    /// <param name="action">The action about to run.</param>
    bool AllowGameRequest(CutsceneActionData action);
}
