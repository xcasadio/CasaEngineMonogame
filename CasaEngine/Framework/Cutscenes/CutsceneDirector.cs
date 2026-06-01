using System.Collections;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Cutscenes;

public sealed class CutsceneDirector
{
    private readonly World _world;
    private readonly List<NavigationAgentComponent> _activeNavigationAgents = [];
    private CutsceneAsset _currentAsset;
    private CutsceneValidationResult _lastValidation = new();
    private CoroutineHandle _activeHandle = CoroutineHandle.Invalid;
    private CutsceneRuntimeState _state = CutsceneRuntimeState.Idle;
    private string _activeActionType;
    private string _activeActionEntityName;
    private Vector3? _activeActionDestination;
    private string _activeActionState;
    private string _activeActionStopReason;

    public CutsceneDirector(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public bool IsPlaying => _activeHandle.IsValid && _world.RuntimeSystems.CoroutineManager.IsRunning(_activeHandle);

    public void Play(CutsceneAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (IsPlaying)
        {
            Stop();
        }

        _currentAsset = asset;
        ResetActiveActionDebug();
        _lastValidation = asset.Validate();
        if (!_lastValidation.IsValid)
        {
            _activeHandle = CoroutineHandle.Invalid;
            _state = CutsceneRuntimeState.Invalid;
            return;
        }

        _state = CutsceneRuntimeState.Playing;
        _activeHandle = _world.RuntimeSystems.CoroutineManager.StartCoroutine(RunAsset(asset), this, $"Cutscene:{asset.Name}");
    }

    public void Stop()
    {
        if (!_activeHandle.IsValid && _state != CutsceneRuntimeState.Playing && !_world.RuntimeSystems.CharacterMotion.HasRequestsFor(this))
        {
            return;
        }

        _world.RuntimeSystems.CoroutineManager.StopAllCoroutines(this);
        _world.RuntimeSystems.CharacterMotion.CancelOwner(this);
        CancelActiveNavigationAgents("Cancelled");
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
                GetActiveCutsceneCoroutines(),
                _activeActionType,
                _activeActionEntityName,
                _activeActionDestination,
                _activeActionState,
                _activeActionStopReason);
    }

    public override string ToString()
    {
        return nameof(CutsceneDirector);
    }

    private IEnumerator RunAsset(CutsceneAsset asset)
    {
        yield return CutsceneActionCoroutineFactory.Create(asset.RootAction!, _world, this);

        _activeHandle = CoroutineHandle.Invalid;
        if (_state == CutsceneRuntimeState.Playing)
        {
            _state = CutsceneRuntimeState.Completed;
        }
    }

    internal bool HasRuntimeFailure => _state == CutsceneRuntimeState.Failed;

    internal void BeginNavigationAction(NavigateToCutsceneActionData action, NavigationAgentComponent navigationAgent)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(navigationAgent);

        AddUniqueNavigationAgent(navigationAgent);
        _activeActionType = action.Type;
        _activeActionEntityName = action.EntityName;
        _activeActionDestination = action.Destination;
        _activeActionState = "Moving";
        _activeActionStopReason = string.Empty;
    }

    internal void UpdateNavigationAction(NavigationAgentComponent navigationAgent, string state, string stopReason)
    {
        ArgumentNullException.ThrowIfNull(navigationAgent);

        _activeActionState = state;
        _activeActionStopReason = stopReason;
    }

    internal void EndNavigationAction(NavigationAgentComponent navigationAgent)
    {
        if (navigationAgent != null)
        {
            _activeNavigationAgents.Remove(navigationAgent);
        }
    }

    internal void MarkRuntimeFailure(string reason)
    {
        _state = CutsceneRuntimeState.Failed;
        _activeActionState = "Failed";
        _activeActionStopReason = reason;
        _activeHandle = CoroutineHandle.Invalid;
        CancelActiveNavigationAgents(reason);
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
        IReadOnlyList<CoroutineDebugInfo> activeCoroutines = _world.RuntimeSystems.CoroutineManager.GetActiveCoroutines();
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

    private void AddUniqueNavigationAgent(NavigationAgentComponent navigationAgent)
    {
        for (int index = 0; index < _activeNavigationAgents.Count; index++)
        {
            if (ReferenceEquals(_activeNavigationAgents[index], navigationAgent))
            {
                return;
            }
        }

        _activeNavigationAgents.Add(navigationAgent);
    }

    private void CancelActiveNavigationAgents(string reason)
    {
        for (int index = 0; index < _activeNavigationAgents.Count; index++)
        {
            _activeNavigationAgents[index].Cancel();
        }

        _activeNavigationAgents.Clear();
        if (!string.IsNullOrWhiteSpace(_activeActionType))
        {
            _activeActionState = string.Equals(reason, "Cancelled", StringComparison.Ordinal) ? "Cancelled" : "Failed";
            _activeActionStopReason = reason;
        }
    }

    private void ResetActiveActionDebug()
    {
        _activeNavigationAgents.Clear();
        _activeActionType = null;
        _activeActionEntityName = null;
        _activeActionDestination = null;
        _activeActionState = null;
        _activeActionStopReason = null;
    }
}