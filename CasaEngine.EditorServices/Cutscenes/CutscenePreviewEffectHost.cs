using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>A request a previewed cutscene addressed to the game and the preview refused.</summary>
/// <param name="Type">The action type, as in <see cref="CutsceneActionTypes"/>.</param>
/// <param name="Target">What the action targets (world, screen, video or next cutscene), or an empty string.</param>
public readonly record struct CutscenePreviewRequest(string Type, string Target);

/// <summary>
/// The <see cref="ICutsceneEffectHost"/> of the cutscene preview (ADR-0135, ADR-0142): a screen effect service of its
/// own that the preview advances, a refusal of every request addressed to the game, the input watch the play armed, and
/// the sound. The refused requests are listed so the interface can report them, and a refused <c>ChangeWorld</c> asks
/// the preview to end the play.
/// </summary>
/// <remarks>
/// The sound actions always get a service, so their durations are the ones of the game. It is the editor audio service
/// only while the sound is on and the step in progress is a normal play step (<see cref="IsNormalPlayStep"/>); for any
/// other step it is a muted service of the host. The host lends the editor service, and can bring the editor mixer back
/// to the state captured at the start of the play (<see cref="TakeMixerSnapshot"/>, <see cref="RestoreEditorAudio"/>).
/// </remarks>
public sealed class CutscenePreviewEffectHost : ICutsceneEffectHost, IDisposable
{
    private readonly List<CutsceneActionData> _refusedActions = [];
    private readonly List<CutscenePreviewRequest> _requests = [];
    private readonly AudioService _editorAudio;
    private AudioService _mutedAudio = CreateMutedAudio();
    private AudioMixerSnapshot _mixerSnapshot;
    private ExitOnInputCutsceneActionData _armedInput;
    private bool _editorAudioLent;
    private bool _isDisposed;

    /// <param name="editorAudio">The audio service of the editor, or null when it has none.</param>
    public CutscenePreviewEffectHost(AudioService editorAudio = null)
    {
        _editorAudio = editorAudio;
    }

    /// <summary>
    /// The audio service the sound actions of the play use. Never null: the editor service when it exists, the sound is on
    /// and <see cref="IsNormalPlayStep"/> is true, otherwise a muted service of the host that plays nothing.
    /// </summary>
    public AudioService Audio
    {
        get
        {
            if (_editorAudio != null && IsSoundOn && IsNormalPlayStep)
            {
                _editorAudioLent = true;
                return _editorAudio;
            }

            return _mutedAudio;
        }
    }

    /// <summary>True when the user turned the sound on. It survives <see cref="Reset"/>.</summary>
    public bool IsSoundOn { get; set; }

    /// <summary>True during a step the transport makes in its running mode. It survives <see cref="Reset"/>.</summary>
    public bool IsNormalPlayStep { get; set; }

    /// <summary>The editor audio service the host can lend, or null.</summary>
    public AudioService EditorAudio => _editorAudio;

    /// <summary>True once <see cref="Audio"/> gave the editor service since the last <see cref="TakeMixerSnapshot"/>.</summary>
    public bool EditorAudioLent => _editorAudioLent;

    /// <summary>The steps counted since the last <see cref="TakeMixerSnapshot"/> that were normal play steps.</summary>
    public int NormalPlayStepCount { get; private set; }

    /// <summary>The steps counted since the last <see cref="TakeMixerSnapshot"/> that were not normal play steps.</summary>
    public int OtherStepCount { get; private set; }

    /// <summary>True when the last step counted was a normal play step; false when it was another one or none was counted.</summary>
    public bool LastStepWasNormalPlay { get; private set; }

    /// <summary>The screen effect service the fades of the play use; advance it with <see cref="Update"/>. The instance never changes.</summary>
    public ScreenEffectService ScreenEffects { get; } = new();

    /// <summary>The requests refused since the last <see cref="Reset"/>, one entry per action.</summary>
    public IReadOnlyList<CutscenePreviewRequest> Requests => _requests;

    /// <summary>True once a <c>ChangeWorld</c> was refused: the game would have replaced the world, the preview must end the play.</summary>
    public bool EndRequested { get; private set; }

    /// <summary>The input watch the play armed (the last <c>ExitOnInput</c> it passed), or null.</summary>
    public ExitOnInputCutsceneActionData ArmedInput => _armedInput;

    /// <summary>The input names of the armed watch, or an empty list.</summary>
    public IReadOnlyList<string> ArmedInputNames => _armedInput != null ? _armedInput.Inputs : [];

    /// <summary>The cutscene the armed watch plays on a press, or an empty string.</summary>
    public string ArmedNextCutscene => _armedInput?.NextCutscene ?? string.Empty;

    /// <summary>Changes only when the armed action changes: arming the same action again, as each turn of a loop does, keeps it.</summary>
    public int ArmedVersion { get; private set; }

    /// <inheritdoc />
    public bool AllowGameRequest(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (action is ChangeWorldCutsceneActionData)
        {
            EndRequested = true;
        }

        if (action is ExitOnInputCutsceneActionData exitOnInput)
        {
            Arm(exitOnInput);
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

    /// <summary>Drops the armed watch, as the end of a play does in the game.</summary>
    public void Disarm()
    {
        if (_armedInput == null)
        {
            return;
        }

        _armedInput = null;
        ArmedVersion++;
    }

    /// <summary>
    /// Says whether <paramref name="inputName"/> is one of the armed inputs and gives the cutscene the game would play.
    /// Changes nothing: it is up to the caller to end the play.
    /// </summary>
    public bool TrySimulateInput(string inputName, out string nextCutscene)
    {
        nextCutscene = null;

        if (_armedInput == null || inputName == null)
        {
            return false;
        }

        for (int i = 0; i < _armedInput.Inputs.Count; i++)
        {
            if (string.Equals(_armedInput.Inputs[i], inputName, StringComparison.Ordinal))
            {
                nextCutscene = ArmedNextCutscene;
                return true;
            }
        }

        return false;
    }

    /// <summary>Advances the fades of the play and the muted audio service, and counts the step by its kind.</summary>
    public void Update(float elapsedSeconds)
    {
        ScreenEffects.Update(elapsedSeconds);
        _mutedAudio.Update(elapsedSeconds);

        LastStepWasNormalPlay = IsNormalPlayStep;
        if (IsNormalPlayStep)
        {
            NormalPlayStepCount++;
        }
        else
        {
            OtherStepCount++;
        }
    }

    /// <summary>
    /// Forgets the refused requests, the end request and the armed watch, puts the screen effect back in its first
    /// state and gives the host a new muted audio service; call it before each new play. It changes neither the editor
    /// audio service, the sound flag, the loan note, the step counts nor the mixer snapshot.
    /// </summary>
    public void Reset()
    {
        _refusedActions.Clear();
        _requests.Clear();
        EndRequested = false;
        Disarm();

        ScreenEffects.SetOverlay(0, 0, 0, 255, SpriteBlendMode.Opaque);
        ScreenEffects.Clear();
        ScreenEffects.Layer = ScreenEffectLayer.BelowUI;

        if (!_isDisposed)
        {
            _mutedAudio.Dispose();
            _mutedAudio = CreateMutedAudio();
        }
    }

    /// <summary>
    /// Captures the editor mixer, forgets that the editor service was lent and zeroes the step counts: call it once per
    /// play, right after the clean up of the previous one. Without an editor service it only zeroes the counts.
    /// </summary>
    public void TakeMixerSnapshot()
    {
        _mixerSnapshot = _editorAudio?.CaptureSnapshot();
        _editorAudioLent = false;
        NormalPlayStepCount = 0;
        OtherStepCount = 0;
        LastStepWasNormalPlay = false;
    }

    /// <summary>
    /// When the editor service was lent since the last <see cref="TakeMixerSnapshot"/>: stops the voices of
    /// <paramref name="voiceOwner"/>, stops the musics and puts the mixer back to the captured state. The note of the loan
    /// stays, so every later stop puts back the same state. A stop, not a pause: nothing resumes.
    /// </summary>
    public void RestoreEditorAudio(object voiceOwner)
    {
        if (_editorAudio == null || !_editorAudioLent)
        {
            return;
        }

        if (voiceOwner != null)
        {
            _editorAudio.StopVoicesOwnedBy(voiceOwner);
        }

        _editorAudio.Music.StopAll(0f);

        if (_mixerSnapshot != null)
        {
            _editorAudio.ApplySnapshot(_mixerSnapshot, 0f);
        }
    }

    /// <summary>Releases the muted audio service. The editor audio service is not owned by the host.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _mutedAudio.Dispose();
    }

    private void Arm(ExitOnInputCutsceneActionData action)
    {
        if (ReferenceEquals(_armedInput, action))
        {
            return;
        }

        _armedInput = action;
        ArmedVersion++;
    }

    private static AudioService CreateMutedAudio() => new(new NullAudioBackend());

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
