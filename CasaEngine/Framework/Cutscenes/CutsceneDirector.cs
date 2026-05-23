using System.Collections;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;

namespace CasaEngine.Framework.Cutscenes;

public sealed class CutsceneDirector
{
    private readonly World _world;
    private CutsceneAsset? _currentAsset;
    private CutsceneValidationResult _lastValidation = new();
    private CoroutineHandle _activeHandle = CoroutineHandle.Invalid;
    private CutsceneRuntimeState _state = CutsceneRuntimeState.Idle;

    public CutsceneDirector(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public bool IsPlaying => _activeHandle.IsValid && _world.CoroutineManager.IsRunning(_activeHandle);

    public void Play(CutsceneAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (IsPlaying)
        {
            Stop();
        }

        _currentAsset = asset;
        _lastValidation = asset.Validate();
        if (!_lastValidation.IsValid)
        {
            _activeHandle = CoroutineHandle.Invalid;
            _state = CutsceneRuntimeState.Invalid;
            return;
        }

        _state = CutsceneRuntimeState.Playing;
        _activeHandle = _world.CoroutineManager.StartCoroutine(RunAsset(asset), this, $"Cutscene:{asset.Name}");
    }

    public void Stop()
    {
        if (!_activeHandle.IsValid && _state != CutsceneRuntimeState.Playing && !_world.CharacterMotion.HasRequestsFor(this))
        {
            return;
        }

        _world.CoroutineManager.StopAllCoroutines(this);
        _world.CharacterMotion.CancelOwner(this);
        _activeHandle = CoroutineHandle.Invalid;
        _state = CutsceneRuntimeState.Stopped;
    }

    public CutsceneDebugSnapshot GetDebugSnapshot()
    {
        return new CutsceneDebugSnapshot(
            _state,
            _currentAsset?.AssetId ?? Guid.Empty,
            _currentAsset?.Name,
            _currentAsset?.FileName,
            _activeHandle,
            CopyValidationMessages(),
            GetActiveCutsceneCoroutines());
    }

    public override string ToString()
    {
        return nameof(CutsceneDirector);
    }

    private IEnumerator RunAsset(CutsceneAsset asset)
    {
        yield return CutsceneActionCoroutineFactory.Create(asset.RootAction!, _world, this);

        _activeHandle = CoroutineHandle.Invalid;
        _state = CutsceneRuntimeState.Completed;
    }

    private IReadOnlyList<CutsceneValidationMessage> CopyValidationMessages()
    {
        if (_lastValidation.Messages.Count == 0)
        {
            return Array.Empty<CutsceneValidationMessage>();
        }

        var messages = new CutsceneValidationMessage[_lastValidation.Messages.Count];
        for (int index = 0; index < _lastValidation.Messages.Count; index++)
        {
            messages[index] = _lastValidation.Messages[index];
        }

        return messages;
    }

    private IReadOnlyList<CoroutineDebugInfo> GetActiveCutsceneCoroutines()
    {
        IReadOnlyList<CoroutineDebugInfo> activeCoroutines = _world.CoroutineManager.GetActiveCoroutines();
        var cutsceneCoroutines = new List<CoroutineDebugInfo>();
        string ownerName = ToString();

        for (int index = 0; index < activeCoroutines.Count; index++)
        {
            CoroutineDebugInfo debugInfo = activeCoroutines[index];
            if (string.Equals(debugInfo.OwnerName, ownerName, StringComparison.Ordinal))
            {
                cutsceneCoroutines.Add(debugInfo);
            }
        }

        return cutsceneCoroutines;
    }
}