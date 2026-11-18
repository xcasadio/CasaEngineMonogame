using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Rendering.ScreenEffects;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>A request a previewed cutscene addressed to the game and the preview refused.</summary>
/// <param name="Type">The action type, as in <see cref="CutsceneActionTypes"/>.</param>
/// <param name="Target">What the action targets (world, screen, video or next cutscene), or an empty string.</param>
public readonly record struct CutscenePreviewRequest(string Type, string Target);

/// <summary>
/// The <see cref="ICutsceneEffectHost"/> of the cutscene preview (ADR-0096): no sound, a screen effect service of its
/// own that the preview advances, and a refusal of every request addressed to the game. The refused requests are listed
/// so the interface can report them, and a refused <c>ChangeWorld</c> asks the preview to end the play.
/// </summary>
public sealed class CutscenePreviewEffectHost : ICutsceneEffectHost
{
    private readonly List<CutsceneActionData> _refusedActions = [];
    private readonly List<CutscenePreviewRequest> _requests = [];

    /// <summary>Always null: the preview plays no sound.</summary>
    public AudioService Audio => null;

    /// <summary>The screen effect service the fades of the play use; advance it with <see cref="Update"/>.</summary>
    public ScreenEffectService ScreenEffects { get; } = new();

    /// <summary>The requests refused since the last <see cref="Reset"/>, one entry per action.</summary>
    public IReadOnlyList<CutscenePreviewRequest> Requests => _requests;

    /// <summary>True once a <c>ChangeWorld</c> was refused: the game would have replaced the world, the preview must end the play.</summary>
    public bool EndRequested { get; private set; }

    /// <inheritdoc />
    public bool AllowGameRequest(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (action is ChangeWorldCutsceneActionData)
        {
            EndRequested = true;
        }

        for (int i = 0; i < _refusedActions.Count; i++)
        {
            if (ReferenceEquals(_refusedActions[i], action))
            {
                return false;
            }
        }

        _refusedActions.Add(action);
        _requests.Add(new CutscenePreviewRequest(action.Type, GetTarget(action)));
        return false;
    }

    /// <summary>Advances the fades of the play.</summary>
    public void Update(float elapsedSeconds) => ScreenEffects.Update(elapsedSeconds);

    /// <summary>Forgets the refused requests and the end request and clears the screen effect; call it before each new play.</summary>
    public void Reset()
    {
        _refusedActions.Clear();
        _requests.Clear();
        EndRequested = false;
        ScreenEffects.Clear();
    }

    private static string GetTarget(CutsceneActionData action)
    {
        string target = action switch
        {
            ChangeWorldCutsceneActionData changeWorld => changeWorld.Target,
            OpenScreenCutsceneActionData openScreen => openScreen.Target,
            PlayVideoCutsceneActionData playVideo => playVideo.Target,
            ExitOnInputCutsceneActionData exitOnInput => exitOnInput.NextCutscene,
            _ => null,
        };

        return target ?? string.Empty;
    }
}
