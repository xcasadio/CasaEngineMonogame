#nullable enable

using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Editor.Runtime;

/// <summary>
/// Plays a cutscene in an isolated copy of a world of the project (ADR-0096). The world is rebuilt from its asset at each
/// play, because stopping a play does not restore the entities. The play runs a copy of the edited asset, so unsaved
/// changes are played and the edited asset is never touched. The play can be paused, advanced by frames and moved to a
/// time by replaying it from the start (ADR-0099); the decisions come from <see cref="CutscenePreviewTransport"/>, the
/// session only runs the steps that it asks for.
/// </summary>
internal sealed class CutscenePreviewSession : IDisposable, ICutscenePreviewStepper
{
    // The first step after a world was rebuilt carries its loading time: it is bounded so that the play does not skip ahead.
    private const float MaxFirstStepSeconds = 1f / 30f;

    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly PreviewWorldDriver _driver;
    private readonly CutscenePreviewEffectHost _host;
    private readonly List<string> _entityNames = [];
    private readonly CutscenePlayTimings _timings = new();
    private readonly CutscenePreviewTransport _transport = new();
    private readonly List<MoveTrack> _moveTracks = [];
    private IReadOnlyDictionary<CutsceneActionData, CutsceneActionData> _copyToOriginal = new Dictionary<CutsceneActionData, CutsceneActionData>();
    private CutsceneDirector? _director;
    private Guid _worldId = Guid.Empty;
    private bool _clampNextStep;
    private bool _disposed;

    // True once the document changed after the play was launched: the play no longer is the one of the document.
    private bool _documentChanged;

    // False until a play was launched in the current world: the measured durations belong to another world before.
    private bool _timingsValid;

    // The asset of the last launch: an action that leaves the marked start state launches it again.
    private CutsceneAsset? _launchedAsset;

    // True while the world shows the first instant of the play (ADR-0100): a zero-duration update was done after the launch.
    // That state is display-only, a play continued from it would be one frame ahead of the play clock.
    private bool _startStateMarked;

    // What the zero-duration update added to the frame count of the director: it is not a frame of the play.
    private long _zeroUpdateFrames;

    public CutscenePreviewSession(HostedEditorGameAdapter editorRuntime)
    {
        ArgumentNullException.ThrowIfNull(editorRuntime);

        _editorRuntime = editorRuntime;
        _host = new CutscenePreviewEffectHost(editorRuntime.AudioSystemComponent?.Service);
        _driver = new PreviewWorldDriver(
            editorRuntime,
            new PreviewWorldDriverOptions
            {
                WorldName = "Cutscene Preview",
                UpdateMode = PreviewWorldUpdateMode.Continuous,
            });
    }

    /// <summary>
    /// How many times a play was launched in this session: each successful rebuild of the world followed by the launch of the
    /// cutscene, an invalid asset included. A stop and a world that fails to load are not counted. Read by the automation.
    /// </summary>
    public int LaunchCount { get; private set; }

    /// <summary>The preview world, or null when none is loaded.</summary>
    public World? World => _driver.World;

    /// <summary>Id of the world asset the preview shows, or <see cref="Guid.Empty"/>.</summary>
    public Guid WorldId => _worldId;

    /// <summary>Why the last <see cref="SetWorld"/> or <see cref="Play"/> could not run, or an empty string.</summary>
    public string Error { get; private set; } = string.Empty;

    /// <summary>State of the last play: Idle before the first one, Playing while it runs, then how it ended.</summary>
    public CutsceneRuntimeState State { get; private set; } = CutsceneRuntimeState.Idle;

    /// <summary>The reason the last play failed, or an empty string.</summary>
    public string FailureReason { get; private set; } = string.Empty;

    /// <summary>True when the last play was ended because the cutscene asked to change the world.</summary>
    public bool EndedByWorldChange { get; private set; }

    /// <summary>True when the last play was ended by <see cref="SimulateInput"/>: the game would have played another cutscene.</summary>
    public bool EndedByInput { get; private set; }

    /// <summary>The input that <see cref="SimulateInput"/> pressed in the last play, or an empty string.</summary>
    public string SimulatedInput { get; private set; } = string.Empty;

    /// <summary>The cutscene the game would have played after <see cref="SimulatedInput"/>, or an empty string.</summary>
    public string SimulatedNextCutscene { get; private set; } = string.Empty;

    /// <summary>The input names of the watch the play armed (its last <c>ExitOnInput</c>), or an empty list. The play drops it when it ends.</summary>
    public IReadOnlyList<string> ArmedInputNames => _host.ArmedInputNames;

    /// <summary>Changes only when the armed watch changes: a loop that arms the same action again keeps it.</summary>
    public int ArmedInputVersion => _host.ArmedVersion;

    /// <summary>The cutscene the armed watch plays on a press, or an empty string.</summary>
    public string ArmedNextCutscene => _host.ArmedNextCutscene;

    /// <summary>True when the user turned the sound of the preview on (ADR-0103). It is off for a new session.</summary>
    public bool IsSoundOn => _host.IsSoundOn;

    /// <summary>The requests to the game that the play made and the preview refused.</summary>
    public IReadOnlyList<CutscenePreviewRequest> Requests => _host.Requests;

    /// <summary>The screen effect of the play: its color is the current fade.</summary>
    public ScreenEffectService ScreenEffects => _host.ScreenEffects;

    public bool IsPlaying => _director?.IsPlaying ?? false;

    /// <summary>How the preview world advances. It only counts while <see cref="IsPlaying"/> is true.</summary>
    public CutscenePreviewTransportMode TransportMode => _transport.Mode;

    /// <summary>The play time that the last <see cref="Seek"/> aimed at, in seconds.</summary>
    public double SeekTargetSeconds => _transport.TargetSeconds;

    /// <summary>Frame steps requested by <see cref="StepFrames"/> that the next <see cref="Tick"/> has still to do.</summary>
    public int PendingFrameSteps => _transport.PendingFrameSteps;

    /// <summary>What the transport row shows (ADR-0099): the play comes first, then the end state of the last play.</summary>
    public CutscenePreviewStatus Status => CutscenePreviewTransport.GetStatus(IsPlaying, _transport.Mode, State, EndedByWorldChange || EndedByInput);

    double ICutscenePreviewStepper.PlayElapsedSeconds => ElapsedSeconds;

    /// <summary>Seconds of the play clock of the last play (ADR-0098); 0 before the first play.</summary>
    public double ElapsedSeconds => _director?.PlayElapsedSeconds ?? 0d;

    /// <summary>Updates of the last play that advanced the clock.</summary>
    public long ElapsedFrames => _director == null ? 0 : Math.Max(0, _director.PlayElapsedFrames - _zeroUpdateFrames);

    /// <summary>The durations measured during the last play, by edited action.</summary>
    public CutscenePlayTimings Timings => _timings;

    /// <summary>Changes each time a measure arrives or the measures are cleared.</summary>
    public int TimingsVersion => _timings.Version;

    /// <summary>Forgets the measured durations: the edited cutscene no longer is the one that was played.</summary>
    public void ClearTimings()
    {
        _timings.Clear();
        _moveTracks.Clear();
        _documentChanged = true;
    }

    /// <summary>
    /// The names of the entities of the preview world, children included, in the order of the world; empty when no world is
    /// loaded. The returned list is reused by every call: read it before calling again. Call it when a list is built, not every frame.
    /// </summary>
    public IReadOnlyList<string> GetEntityNames()
    {
        _entityNames.Clear();
        var world = _driver.World;
        if (world != null)
        {
            AppendEntityNames(world.Entities);
        }

        return _entityNames;
    }

    private void AppendEntityNames(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            if (!string.IsNullOrEmpty(entity.Name))
            {
                _entityNames.Add(entity.Name);
            }

            AppendEntityNames(entity.Children);
        }
    }

    /// <summary>Loads a copy of a world asset as the preview world. False, with <see cref="Error"/>, when it cannot be loaded.</summary>
    public bool SetWorld(Guid worldAssetId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Stop cleans the editor audio even when nothing plays: the voices belong to the world that is about to be replaced.
        Stop();
        DetachDirector();
        _worldId = Guid.Empty;
        Error = string.Empty;
        State = CutsceneRuntimeState.Idle;
        FailureReason = string.Empty;
        EndedByWorldChange = false;
        ClearSimulatedInput();
        _host.Reset();
        _timingsValid = false;

        if (worldAssetId == Guid.Empty)
        {
            _driver.Clear();
            return false;
        }

        if (!TryLoadWorld(worldAssetId))
        {
            return false;
        }

        _worldId = worldAssetId;
        return true;
    }

    /// <summary>
    /// Rebuilds the world from its asset and plays a copy of <paramref name="asset"/> in it, from the start, with the
    /// measured durations forgotten. False, with <see cref="Error"/>, when no world is set or it cannot be rebuilt.
    /// </summary>
    public bool Play(CutsceneAsset asset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(asset);

        return Launch(asset, keepTimings: false);
    }

    /// <summary>Freezes the play. Does nothing when no play runs. A seek in progress is abandoned at the time it reached.</summary>
    public void Pause()
    {
        if (IsPlaying)
        {
            _transport.Pause();
            RestoreEditorAudio();
        }
    }

    /// <summary>
    /// Turns the sound of the preview on or off (ADR-0103). Turning it off stops what the preview made heard and puts the
    /// editor mixer back. The mixer is never captured here, only once per launch. Does nothing when the state is the same.
    /// </summary>
    public void SetSound(bool on)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_host.IsSoundOn == on)
        {
            return;
        }

        _host.IsSoundOn = on;
        if (!on)
        {
            RestoreEditorAudio();
        }
    }

    /// <summary>
    /// Simulates the press of an input of the armed watch (ADR-0103): the play ends, and <see cref="SimulatedInput"/> and
    /// <see cref="SimulatedNextCutscene"/> say what the game would have done. The next cutscene is not played. Acts only
    /// while a play runs or is paused, not while it seeks. False when it did nothing.
    /// </summary>
    public bool SimulateInput(string inputName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsPlaying || _transport.Mode == CutscenePreviewTransportMode.Seeking)
        {
            return false;
        }

        // The armed watch and the next cutscene are read before the stop: the end of the play drops the watch.
        if (!_host.TrySimulateInput(inputName, out string? nextCutscene))
        {
            return false;
        }

        SimulatedInput = inputName;
        SimulatedNextCutscene = nextCutscene ?? string.Empty;
        EndedByInput = true;
        _director!.Stop();
        return true;
    }

    /// <summary>
    /// Lets a paused play advance again. Does nothing when no play runs or the play is not paused. From the marked start
    /// state the play is launched again, without the zero-duration update (ADR-0100). True when the world was rebuilt.
    /// </summary>
    public bool Resume()
    {
        if (!IsPlaying || _transport.Mode != CutscenePreviewTransportMode.Paused)
        {
            return false;
        }

        bool rebuilt = LeaveStartState();
        if (!rebuilt || IsPlaying)
        {
            _transport.Resume();
        }

        return rebuilt;
    }

    /// <summary>
    /// Pauses the play and advances it by <paramref name="count"/> frames. Does nothing when no play runs. From the marked
    /// start state the play is launched again first, without the zero-duration update (ADR-0100). True when the world was rebuilt.
    /// </summary>
    public bool StepFrames(int count)
    {
        if (!IsPlaying)
        {
            return false;
        }

        RestoreEditorAudio();
        bool rebuilt = LeaveStartState();
        if (!rebuilt || IsPlaying)
        {
            _transport.StepFrames(count);
        }

        return rebuilt;
    }

    /// <summary>When the marked start state is in place, launches the last asset again, keeping the measured durations.</summary>
    private bool LeaveStartState()
    {
        return _startStateMarked && _launchedAsset != null && Launch(_launchedAsset, keepTimings: true);
    }

    /// <summary>
    /// Moves the preview to a play time and pauses it there (ADR-0099). When the time is ahead of the play, the play runs
    /// and the document did not change since it was launched, the play goes on from its current state. Otherwise the world
    /// is rebuilt and a copy of <paramref name="asset"/> is played again from the start, keeping the measured durations.
    /// The first instant (a time less than half a step) is shown by one zero-duration update after the launch, which is not
    /// counted in <see cref="ElapsedFrames"/>; that start state is marked and no play goes on from it (ADR-0100): the next
    /// action that leaves it launches the play again, without the zero-duration update. The steps are done by
    /// <see cref="Tick"/>, within a time budget per frame. False, with <see cref="Error"/>, when no
    /// world is set or it cannot be rebuilt.
    /// </summary>
    public bool Seek(CutsceneAsset asset, double seconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(asset);

        double target = Math.Max(0d, seconds);
        bool showsFirstInstant = target < CutscenePreviewTransport.StepSeconds / 2.0;
        if (IsPlaying && !_documentChanged)
        {
            bool inPlace = _startStateMarked
                ? showsFirstInstant
                : target >= ElapsedSeconds - CutscenePreviewTransport.StepSeconds / 2.0;
            if (inPlace)
            {
                RestoreEditorAudio();
                _transport.Seek(target, ElapsedSeconds);
                return true;
            }
        }

        if (!Launch(asset, keepTimings: true))
        {
            return false;
        }

        if (showsFirstInstant)
        {
            ShowFirstInstant();
        }

        _transport.Seek(target, ElapsedSeconds);
        return true;
    }

    /// <summary>
    /// Puts the preview back in its state before any play: stops the play, forgets what it requested, rebuilds the world
    /// from its asset and sets the state to Stopped, with the play time at 0. The measured durations are kept. False, with
    /// <see cref="Error"/>, when no world is set or it cannot be rebuilt.
    /// </summary>
    public bool Rewind()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Stop();
        if (_worldId == Guid.Empty)
        {
            Error = "No preview world is selected.";
            return false;
        }

        _host.Reset();
        EndedByWorldChange = false;
        ClearSimulatedInput();
        FailureReason = string.Empty;
        State = CutsceneRuntimeState.Idle;
        if (!TryLoadWorld(_worldId))
        {
            return false;
        }

        // A play that was already over raised no Ended when it was stopped: the state is set here.
        State = CutsceneRuntimeState.Stopped;
        _transport.Start();
        return true;
    }

    /// <summary>
    /// Does one zero-duration update, by the path of a normal step, so that the actions of time 0 play, and marks the start
    /// state. What the update added to the frame count of the director is taken off <see cref="ElapsedFrames"/>.
    /// </summary>
    private void ShowFirstInstant()
    {
        if (_director == null || !_director.IsPlaying)
        {
            return;
        }

        long before = _director.PlayElapsedFrames;
        ((ICutscenePreviewStepper)this).Step(0f);
        _zeroUpdateFrames = _director.PlayElapsedFrames - before;
        _startStateMarked = _director.IsPlaying;
    }

    private bool Launch(CutsceneAsset asset, bool keepTimings)
    {
        // Stop ends the previous play and cleans the editor audio, with the previous world as the owner of the voices; only
        // then is the mixer captured, once for this launch, whether the sound is on or not.
        Stop();
        if (_worldId == Guid.Empty)
        {
            Error = "No preview world is selected.";
            return false;
        }

        _host.TakeMixerSnapshot();
        _host.Reset();
        EndedByWorldChange = false;
        ClearSimulatedInput();
        FailureReason = string.Empty;
        State = CutsceneRuntimeState.Idle;
        if (!TryLoadWorld(_worldId))
        {
            return false;
        }

        var copy = CopyAsset(asset);
        _moveTracks.Clear();
        _copyToOriginal = copy.RootAction != null && asset.RootAction != null
            ? CutsceneActionTree.MapByShape(copy.RootAction, asset.RootAction) ?? _copyToOriginal
            : _copyToOriginal;
        if (keepTimings && _timingsValid)
        {
            _timings.Continue(_copyToOriginal);
        }
        else
        {
            _timings.Begin(_copyToOriginal);
        }

        _timingsValid = true;
        _documentChanged = false;
        _launchedAsset = asset;
        _transport.Start();
        _director!.Play(copy);
        LaunchCount++;
        if (!_director.IsPlaying && State == CutsceneRuntimeState.Idle)
        {
            // An invalid asset starts nothing and raises no Ended.
            State = _director.GetDebugSnapshot().State;
        }
        else if (_director.IsPlaying)
        {
            State = CutsceneRuntimeState.Playing;
        }

        return true;
    }

    /// <summary>Stops the play in progress, if any. The world keeps the state the play left.</summary>
    public void Stop()
    {
        _director?.Stop();
        RestoreEditorAudio();
    }

    /// <summary>
    /// Advances the world, so the play, and the fades of the play, as the transport decides: with the frame when it runs,
    /// by fixed steps within a time budget when it seeks, not at all when it is paused.
    /// </summary>
    public void Tick(GameTime gameTime)
    {
        ArgumentNullException.ThrowIfNull(gameTime);

        if (_disposed || _driver.World == null)
        {
            return;
        }

        float frameSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_clampNextStep && frameSeconds > MaxFirstStepSeconds)
        {
            frameSeconds = MaxFirstStepSeconds;
        }

        _clampNextStep = false;

        // Only a step that the transport makes while it runs may be heard: a seek, a frame step and the zero-duration
        // update of the first instant (done outside this method) would play every sound of their path at once.
        _host.IsNormalPlayStep = _transport.Mode == CutscenePreviewTransportMode.Running;
        _transport.Advance(this, frameSeconds);
        _host.IsNormalPlayStep = false;
    }

    void ICutscenePreviewStepper.Step(float seconds)
    {
        _driver.Tick(seconds);
        _host.Update(seconds);
        UpdateMoveTracks();

        if (_host.EndRequested && _director != null && _director.IsPlaying)
        {
            EndedByWorldChange = true;
            _director.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RestoreEditorAudio();
        DetachDirector();
        _host.Dispose();
        _driver.Dispose();
    }

    private bool TryLoadWorld(Guid worldAssetId)
    {
        DetachDirector();

        // The new director counts its frames from 0.
        _startStateMarked = false;
        _zeroUpdateFrames = 0;

        try
        {
            _driver.Rebuild(() => _editorRuntime.AssetContentManager.LoadCopy<World>(worldAssetId));
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            _driver.Clear();
            Error = $"Cannot load the preview world: {exception.Message}";
            return false;
        }

        _director = _driver.World!.CutsceneDirector;
        _director.EffectHost = _host;
        _director.Ended += OnEnded;
        _director.ActionStarted += OnActionStarted;
        _director.ActionEnded += OnActionEnded;
        _clampNextStep = true;
        Error = string.Empty;
        return true;
    }

    private void DetachDirector()
    {
        if (_director != null)
        {
            _director.Ended -= OnEnded;
            _director.ActionStarted -= OnActionStarted;
            _director.ActionEnded -= OnActionEnded;
            _director = null;
            _moveTracks.Clear();
        }
    }

    private void OnEnded(object? sender, CutsceneEndedEventArgs e)
    {
        State = e.State;
        FailureReason = e.State == CutsceneRuntimeState.Failed
            ? _director?.GetDebugSnapshot().ActiveActionStopReason ?? string.Empty
            : string.Empty;

        // As in the game, the input watch does not outlive its play, and what the play made heard stops with it.
        _host.Disarm();
        RestoreEditorAudio();
    }

    /// <summary>Puts the editor audio back to the state captured at the launch, with the preview world as the owner of the voices.</summary>
    private void RestoreEditorAudio()
    {
        _host.RestoreEditorAudio(_driver.World);
    }

    private void ClearSimulatedInput()
    {
        EndedByInput = false;
        SimulatedInput = string.Empty;
        SimulatedNextCutscene = string.Empty;
    }

    private void OnActionStarted(in CutsceneActionNotification notification)
    {
        _timings.OnStarted(in notification);

        if (notification.Action is not MoveSpriteCutsceneActionData move
            || !_copyToOriginal.TryGetValue(move, out var original)
            || _timings.TryGetDuration(original, out _)
            || IsTracked(original))
        {
            return;
        }

        var world = _driver.World;
        if (world == null)
        {
            return;
        }

        for (int index = 0; index < world.Entities.Count; index++)
        {
            var entity = world.Entities[index];
            if (string.Equals(entity.Name, move.EntityName, StringComparison.Ordinal))
            {
                if (entity.RootComponent is StaticSpriteComponent sprite)
                {
                    _moveTracks.Add(new MoveTrack(sprite, original, notification.ElapsedSeconds));
                }

                return;
            }
        }
    }

    private void OnActionEnded(in CutsceneActionNotification notification)
    {
        _timings.OnEnded(in notification);
    }

    private bool IsTracked(CutsceneActionData original)
    {
        for (int index = 0; index < _moveTracks.Count; index++)
        {
            if (ReferenceEquals(_moveTracks[index].Action, original))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A move effect ends when its sprite stops moving. The check runs after the world update that started the move, in
    /// which StartMove was already called, so a sprite that is not moving has finished.
    /// </summary>
    private void UpdateMoveTracks()
    {
        if (_director == null)
        {
            return;
        }

        for (int index = _moveTracks.Count - 1; index >= 0; index--)
        {
            var track = _moveTracks[index];
            if (track.Sprite.IsMoving)
            {
                continue;
            }

            _timings.SetEffectDuration(track.Action, Math.Max(0d, _director.PlayElapsedSeconds - track.StartSeconds));
            _moveTracks.RemoveAt(index);
        }
    }

    private readonly record struct MoveTrack(StaticSpriteComponent Sprite, CutsceneActionData Action, double StartSeconds);

    private static CutsceneAsset CopyAsset(CutsceneAsset asset)
    {
        var node = new JObject();
        CutsceneAssetJsonSerializer.Save(asset, node);

        var copy = new CutsceneAsset
        {
            Name = asset.Name,
            AssetId = asset.AssetId,
            FileName = asset.FileName,
        };
        CutsceneAssetJsonSerializer.Load(copy, node);
        return copy;
    }
}
