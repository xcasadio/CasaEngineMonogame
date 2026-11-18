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
/// changes are played and the edited asset is never touched.
/// </summary>
internal sealed class CutscenePreviewSession : IDisposable
{
    // The first step after a world was rebuilt carries its loading time: it is bounded so that the play does not skip ahead.
    private const float MaxFirstStepSeconds = 1f / 30f;

    private readonly HostedEditorGameAdapter _editorRuntime;
    private readonly PreviewWorldDriver _driver;
    private readonly CutscenePreviewEffectHost _host = new();
    private readonly List<string> _entityNames = [];
    private readonly CutscenePlayTimings _timings = new();
    private readonly List<MoveTrack> _moveTracks = [];
    private IReadOnlyDictionary<CutsceneActionData, CutsceneActionData> _copyToOriginal = new Dictionary<CutsceneActionData, CutsceneActionData>();
    private CutsceneDirector? _director;
    private Guid _worldId = Guid.Empty;
    private bool _clampNextStep;
    private bool _disposed;

    public CutscenePreviewSession(HostedEditorGameAdapter editorRuntime)
    {
        ArgumentNullException.ThrowIfNull(editorRuntime);

        _editorRuntime = editorRuntime;
        _driver = new PreviewWorldDriver(
            editorRuntime,
            new PreviewWorldDriverOptions
            {
                WorldName = "Cutscene Preview",
                UpdateMode = PreviewWorldUpdateMode.Continuous,
            });
    }

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

    /// <summary>The requests to the game that the play made and the preview refused.</summary>
    public IReadOnlyList<CutscenePreviewRequest> Requests => _host.Requests;

    /// <summary>The screen effect of the play: its color is the current fade.</summary>
    public ScreenEffectService ScreenEffects => _host.ScreenEffects;

    public bool IsPlaying => _director?.IsPlaying ?? false;

    /// <summary>Seconds of the play clock of the last play (ADR-0098); 0 before the first play.</summary>
    public double ElapsedSeconds => _director?.PlayElapsedSeconds ?? 0d;

    /// <summary>Updates of the last play that advanced the clock.</summary>
    public long ElapsedFrames => _director?.PlayElapsedFrames ?? 0;

    /// <summary>The durations measured during the last play, by edited action.</summary>
    public CutscenePlayTimings Timings => _timings;

    /// <summary>Changes each time a measure arrives or the measures are cleared.</summary>
    public int TimingsVersion => _timings.Version;

    /// <summary>Forgets the measured durations: the edited cutscene no longer is the one that was played.</summary>
    public void ClearTimings()
    {
        _timings.Clear();
        _moveTracks.Clear();
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

        Stop();
        DetachDirector();
        _worldId = Guid.Empty;
        Error = string.Empty;
        State = CutsceneRuntimeState.Idle;
        FailureReason = string.Empty;
        EndedByWorldChange = false;
        _host.Reset();

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
    /// Rebuilds the world from its asset and plays a copy of <paramref name="asset"/> in it. False, with <see cref="Error"/>,
    /// when no world is set or it cannot be rebuilt.
    /// </summary>
    public bool Play(CutsceneAsset asset)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(asset);

        Stop();
        if (_worldId == Guid.Empty)
        {
            Error = "No preview world is selected.";
            return false;
        }

        _host.Reset();
        EndedByWorldChange = false;
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
        _timings.Begin(_copyToOriginal);
        _director!.Play(copy);
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
    }

    /// <summary>Advances the world, so the play, and the fades of the play.</summary>
    public void Tick(GameTime gameTime)
    {
        ArgumentNullException.ThrowIfNull(gameTime);

        if (_disposed || _driver.World == null)
        {
            return;
        }

        if (_clampNextStep && gameTime.ElapsedGameTime.TotalSeconds > MaxFirstStepSeconds)
        {
            gameTime = new GameTime(gameTime.TotalGameTime, TimeSpan.FromSeconds(MaxFirstStepSeconds));
        }

        _clampNextStep = false;
        _driver.Tick(gameTime);
        _host.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
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
        DetachDirector();
        _driver.Dispose();
    }

    private bool TryLoadWorld(Guid worldAssetId)
    {
        DetachDirector();

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
