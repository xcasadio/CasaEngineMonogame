using System.Collections;
using CasaEngine.Core.Logging;
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
    private int _playToken;
    private bool _endRaised = true;

    public CutsceneDirector(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// <summary>
    /// Raised once per started play, after the state is set: with Completed or Failed when the play finishes,
    /// with Stopped when a running play is stopped (including by <c>World.Clear</c> or a replacing <see cref="Play"/>).
    /// A play of an invalid asset starts nothing and raises nothing.
    /// Failed is raised once the play unwinds to its end, after every branch it still runs is stopped, or when a failed
    /// play is stopped or replaced before that.
    /// An exception thrown by a handler is logged with its context, like a coroutine exception, and does not interrupt
    /// the director; it is rethrown only when <see cref="CoroutineManager.ThrowCoroutineExceptionsInDebug"/> is set.
    /// A handler may call <see cref="Play"/>: when it does so while a replacing <see cref="Play"/> stops the previous
    /// play, the play started by the handler stands and the replacing call starts nothing.
    /// </summary>
    public event EventHandler<CutsceneEndedEventArgs> Ended;

    public bool IsPlaying => _activeHandle.IsValid && _world.RuntimeSystems.CoroutineManager.IsRunning(_activeHandle);

    public void Play(CutsceneAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (IsPlaying || IsFailureAwaitingEnd)
        {
            int tokenBeforeStop = _playToken;
            Stop();
            if (tokenBeforeStop != _playToken)
            {
                // An Ended handler re-entered Play while the previous play was being stopped: that newer play stands.
                return;
            }
        }

        _currentAsset = asset;
        _playToken++;
        _endRaised = true;
        ResetActiveActionDebug();
        _lastValidation = asset.Validate();
        if (!_lastValidation.IsValid)
        {
            _activeHandle = CoroutineHandle.Invalid;
            _state = CutsceneRuntimeState.Invalid;
            return;
        }

        _state = CutsceneRuntimeState.Playing;
        _endRaised = false;
        _activeHandle = _world.RuntimeSystems.CoroutineManager.StartCoroutine(RunAsset(asset, _playToken), this, $"Cutscene:{asset.Name}");
    }

    public void Stop()
    {
        bool failureAwaitingEnd = IsFailureAwaitingEnd;
        if (!_activeHandle.IsValid && _state != CutsceneRuntimeState.Playing && !failureAwaitingEnd && !_world.RuntimeSystems.CharacterMotion.HasRequestsFor(this))
        {
            return;
        }

        // A failed play keeps the reason of its failure in the debug state; a stopped one is cancelled.
        StopOwnedWork(failureAwaitingEnd ? _activeActionStopReason : "Cancelled");
        _activeHandle = CoroutineHandle.Invalid;
        if (!failureAwaitingEnd)
        {
            _state = CutsceneRuntimeState.Stopped;
        }

        // A failed play torn down before it unwound still owes its Ended, and it is Failed, not Stopped.
        RaiseEnded(_state);
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

    private IEnumerator RunAsset(CutsceneAsset asset, int playToken)
    {
        yield return CutsceneActionCoroutineFactory.Create(asset.RootAction!, _world, this, playToken);

        if (playToken != _playToken)
        {
            // A newer play owns the director state now (started from an Ended handler).
            yield break;
        }

        _activeHandle = CoroutineHandle.Invalid;
        if (_state == CutsceneRuntimeState.Playing)
        {
            _state = CutsceneRuntimeState.Completed;
        }
        else if (_state == CutsceneRuntimeState.Failed)
        {
            // A parallel branch started after the failing one may still run: nothing of a failed play outlives its end.
            StopOwnedWork(_activeActionStopReason);
        }

        RaiseEnded(_state);
    }

    private void StopOwnedWork(string navigationStopReason)
    {
        _world.RuntimeSystems.CoroutineManager.StopAllCoroutines(this);
        _world.RuntimeSystems.CharacterMotion.CancelOwner(this);
        CancelActiveNavigationAgents(navigationStopReason);
    }

    private void RaiseEnded(CutsceneRuntimeState state)
    {
        if (_endRaised)
        {
            return;
        }

        _endRaised = true;
        EventHandler<CutsceneEndedEventArgs> handler = Ended;
        if (handler == null)
        {
            return;
        }

        CutsceneAsset asset = _currentAsset;
        try
        {
            handler(this, new CutsceneEndedEventArgs(state, asset));
        }
        catch (Exception exception)
        {
            // Treated like an exception thrown by a coroutine: logged with its context, so that Stop, a replacing Play
            // and World.Clear always finish their work.
            Logs.WriteException(new Exception($"A handler of CutsceneDirector.Ended threw for cutscene '{asset?.Name}' ({state}).", exception));
            if (_world.RuntimeSystems.CoroutineManager.ThrowCoroutineExceptionsInDebug)
            {
                throw;
            }
        }
    }

    private bool IsFailureAwaitingEnd => _state == CutsceneRuntimeState.Failed && !_endRaised;

    /// <summary>True once the play identified by <paramref name="playToken"/> failed or was superseded by a newer play.</summary>
    internal bool IsPlayAborted(int playToken) => playToken != _playToken || _state == CutsceneRuntimeState.Failed;

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

    internal void MarkRuntimeFailure(string reason, int playToken)
    {
        if (playToken != _playToken)
        {
            // A superseded play must not fail the one that replaced it.
            return;
        }

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