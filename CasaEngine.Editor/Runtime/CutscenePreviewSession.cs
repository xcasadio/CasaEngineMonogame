#nullable enable

using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using CasaEngine.Framework.Rendering.ScreenEffects;
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

        _director!.Play(CopyAsset(asset));
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
        _clampNextStep = true;
        Error = string.Empty;
        return true;
    }

    private void DetachDirector()
    {
        if (_director != null)
        {
            _director.Ended -= OnEnded;
            _director = null;
        }
    }

    private void OnEnded(object? sender, CutsceneEndedEventArgs e)
    {
        State = e.State;
        FailureReason = e.State == CutsceneRuntimeState.Failed
            ? _director?.GetDebugSnapshot().ActiveActionStopReason ?? string.Empty
            : string.Empty;
    }

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
