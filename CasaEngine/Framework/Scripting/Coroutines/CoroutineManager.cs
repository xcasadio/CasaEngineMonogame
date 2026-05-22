using System.Collections;
using System.Threading;
using CasaEngine.Core.Logging;

namespace CasaEngine.Framework.Scripting.Coroutines;

public sealed class CoroutineManager : ICoroutineManager
{
    private static int _nextManagerId;

    private readonly List<CoroutineInstance?> _slots = [];
    private readonly List<int> _activeSlots = [];
    private readonly Queue<int> _pendingStartSlots = new();
    private readonly Stack<int> _freeSlots = new();
    private int _nextGeneration = 1;

    public CoroutineManager()
    {
        ManagerId = Interlocked.Increment(ref _nextManagerId);
    }

    public int ManagerId { get; }

    public bool ThrowCoroutineExceptionsInDebug { get; set; }

    public CoroutineHandle StartCoroutine(IEnumerator routine)
    {
        return StartCoroutine(routine, null);
    }

    public CoroutineHandle StartCoroutine(IEnumerator routine, object? owner)
    {
        return StartCoroutine(routine, owner, null);
    }

    public CoroutineHandle StartCoroutine(IEnumerator routine, object? owner, string? name)
    {
        ArgumentNullException.ThrowIfNull(routine);

        int slot = AllocateSlot();
        var handle = new CoroutineHandle(ManagerId, slot, NextGeneration());
        _slots[slot] = new CoroutineInstance(handle, routine, owner)
        {
            Name = name
        };
        _pendingStartSlots.Enqueue(slot);
        return handle;
    }

    public void StopCoroutine(CoroutineHandle handle)
    {
        if (TryGetInstance(handle, out CoroutineInstance? instance))
        {
            instance.IsStopped = true;
            instance.CurrentYield = null;
        }
    }

    public void StopAllCoroutines()
    {
        for (int index = 0; index < _slots.Count; index++)
        {
            CoroutineInstance? instance = _slots[index];
            if (instance != null)
            {
                instance.IsStopped = true;
                instance.CurrentYield = null;
            }
        }
    }

    public void StopAllCoroutines(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        for (int index = 0; index < _slots.Count; index++)
        {
            CoroutineInstance? instance = _slots[index];
            if (instance != null && ReferenceEquals(instance.Owner, owner))
            {
                instance.IsStopped = true;
                instance.CurrentYield = null;
            }
        }
    }

    public bool IsRunning(CoroutineHandle handle)
    {
        return TryGetInstance(handle, out CoroutineInstance? instance)
            && !instance.IsStopped
            && !instance.IsCompleted;
    }

    public void SetCoroutineName(CoroutineHandle handle, string? name)
    {
        if (TryGetInstance(handle, out CoroutineInstance? instance))
        {
            instance.Name = name;
        }
    }

    public IReadOnlyList<CoroutineDebugInfo> GetActiveCoroutines()
    {
        var debugInfos = new List<CoroutineDebugInfo>();

        for (int index = 0; index < _slots.Count; index++)
        {
            CoroutineInstance? instance = _slots[index];
            if (instance == null || instance.IsStopped || instance.IsCompleted)
            {
                continue;
            }

            string state = GetDebugState(instance);
            debugInfos.Add(new CoroutineDebugInfo
            {
                Id = instance.Handle.Slot,
                Handle = instance.Handle,
                Name = instance.Name,
                OwnerName = GetOwnerName(instance.Owner),
                CurrentInstruction = GetCurrentInstructionName(instance),
                IsPaused = state == CoroutineDebugStates.Waiting,
                RemainingTime = GetRemainingTime(instance.CurrentInstruction),
                State = state
            });
        }

        return debugInfos;
    }

    public void Update(CoroutineUpdateContext context)
    {
        AddPendingCoroutines();

        int activeCount = _activeSlots.Count;
        for (int index = 0; index < activeCount; index++)
        {
            CoroutineInstance? instance = _slots[_activeSlots[index]];
            if (instance == null || instance.IsStopped || instance.IsCompleted)
            {
                continue;
            }

            try
            {
                UpdateCoroutine(instance, context);
            }
            catch (Exception exception)
            {
                instance.Fault = exception;
                instance.IsStopped = true;
                Logs.WriteException(new Exception(CreateExceptionMessage(instance), exception));

                if (ThrowCoroutineExceptionsInDebug)
                {
                    RemoveCompletedAndStoppedCoroutines();
                    throw;
                }
            }
        }

        RemoveCompletedAndStoppedCoroutines();
    }

    private void AddPendingCoroutines()
    {
        while (_pendingStartSlots.Count > 0)
        {
            int slot = _pendingStartSlots.Dequeue();
            CoroutineInstance? instance = _slots[slot];
            if (instance == null)
            {
                continue;
            }

            if (instance.IsStopped || instance.IsCompleted)
            {
                ReleaseSlot(slot, instance);
                continue;
            }

            _activeSlots.Add(slot);
        }
    }

    private void UpdateCoroutine(CoroutineInstance coroutine, CoroutineUpdateContext context)
    {
        if (coroutine.CurrentInstruction != null)
        {
            if (!coroutine.CurrentInstruction.IsCompleted(context))
            {
                return;
            }

            coroutine.CurrentInstruction = null;
            coroutine.CurrentYield = null;
        }

        if (coroutine.WaitingHandle.IsValid)
        {
            if (IsRunning(coroutine.WaitingHandle))
            {
                return;
            }

            coroutine.WaitingHandle = CoroutineHandle.Invalid;
            coroutine.CurrentYield = null;
        }

        if (coroutine.ResumeFrameIndex >= 0)
        {
            if (context.FrameIndex < coroutine.ResumeFrameIndex)
            {
                return;
            }

            coroutine.ResumeFrameIndex = -1;
            coroutine.CurrentYield = null;
        }

        while (coroutine.Stack.Count > 0)
        {
            IEnumerator current = coroutine.Stack.Peek();
            if (!current.MoveNext())
            {
                CoroutineInstance.DisposeEnumerator(coroutine.Stack.Pop());
                continue;
            }

            object? yielded = current.Current;
            coroutine.CurrentYield = yielded;

            if (yielded == null)
            {
                coroutine.ResumeFrameIndex = context.FrameIndex + 1;
                return;
            }

            if (yielded is IEnumerator nestedEnumerator)
            {
                coroutine.Stack.Push(nestedEnumerator);
                continue;
            }

            if (yielded is ICoroutineInstruction instruction)
            {
                coroutine.CurrentInstruction = instruction;
                return;
            }

            if (yielded is CoroutineHandle handle)
            {
                if (TryBeginHandleWait(coroutine, handle))
                {
                    return;
                }

                continue;
            }

            throw new InvalidOperationException($"Unsupported coroutine yield type: {yielded.GetType().FullName}");
        }

        coroutine.CurrentYield = null;
        coroutine.IsCompleted = true;
    }

    private bool TryBeginHandleWait(CoroutineInstance coroutine, CoroutineHandle handle)
    {
        if (!handle.IsValid)
        {
            HandleInvalidYieldedHandle(coroutine, handle, "invalid handle");
            return false;
        }

        if (handle == coroutine.Handle)
        {
            throw new InvalidOperationException("A coroutine cannot wait for its own handle.");
        }

        if (handle.ManagerId != ManagerId)
        {
            HandleInvalidYieldedHandle(coroutine, handle, "handle belongs to another CoroutineManager");
            return false;
        }

        if (!IsRunning(handle))
        {
            return false;
        }

        coroutine.WaitingHandle = handle;
        return true;
    }

    private void HandleInvalidYieldedHandle(CoroutineInstance coroutine, CoroutineHandle handle, string reason)
    {
        string message = $"Coroutine yielded an unsupported CoroutineHandle ({reason}): "
            + $"{handle.ManagerId}/{handle.Slot}/{handle.Generation}.";

        if (ThrowCoroutineExceptionsInDebug)
        {
            throw new InvalidOperationException(message);
        }

        Logs.WriteWarning(message);
        coroutine.CurrentYield = null;
    }

    private void RemoveCompletedAndStoppedCoroutines()
    {
        for (int index = _activeSlots.Count - 1; index >= 0; index--)
        {
            int slot = _activeSlots[index];
            CoroutineInstance? instance = _slots[slot];
            if (instance == null || instance.IsStopped || instance.IsCompleted)
            {
                _activeSlots.RemoveAt(index);

                if (instance != null)
                {
                    ReleaseSlot(slot, instance);
                }
            }
        }
    }

    private bool TryGetInstance(CoroutineHandle handle, out CoroutineInstance? instance)
    {
        instance = null;
        if (!handle.IsValid || handle.ManagerId != ManagerId || handle.Slot >= _slots.Count)
        {
            return false;
        }

        CoroutineInstance? candidate = _slots[handle.Slot];
        if (candidate == null || candidate.Handle.Generation != handle.Generation)
        {
            return false;
        }

        instance = candidate;
        return true;
    }

    private int AllocateSlot()
    {
        if (_freeSlots.Count > 0)
        {
            return _freeSlots.Pop();
        }

        _slots.Add(null);
        return _slots.Count - 1;
    }

    private int NextGeneration()
    {
        int generation = _nextGeneration;
        _nextGeneration++;
        if (_nextGeneration == int.MaxValue)
        {
            _nextGeneration = 1;
        }

        return generation;
    }

    private void ReleaseSlot(int slot, CoroutineInstance instance)
    {
        instance.Dispose();
        _slots[slot] = null;
        _freeSlots.Push(slot);
    }

    private static string CreateExceptionMessage(CoroutineInstance instance)
    {
        string name = string.IsNullOrWhiteSpace(instance.Name) ? "<unnamed>" : instance.Name;
        string owner = instance.Owner?.ToString() ?? "<none>";
        return $"Coroutine '{name}' failed. Owner: {owner}. Handle: {instance.Handle.ManagerId}/{instance.Handle.Slot}/{instance.Handle.Generation}.";
    }

    private static string? GetOwnerName(object? owner)
    {
        return owner switch
        {
            null => null,
            ObjectBase objectBase => objectBase.Name,
            _ => owner.ToString()
        };
    }

    private static string? GetCurrentInstructionName(CoroutineInstance instance)
    {
        if (instance.CurrentInstruction != null)
        {
            return instance.CurrentInstruction.GetType().Name;
        }

        if (instance.WaitingHandle.IsValid)
        {
            return nameof(CoroutineHandle);
        }

        if (instance.ResumeFrameIndex >= 0)
        {
            return "NextFrame";
        }

        return instance.CurrentYield?.GetType().Name;
    }

    private static float? GetRemainingTime(ICoroutineInstruction? instruction)
    {
        return instruction switch
        {
            WaitForSeconds waitForSeconds => MathF.Max(0f, waitForSeconds.RemainingTime),
            WaitForSecondsRealtime waitForSecondsRealtime => MathF.Max(0f, waitForSecondsRealtime.RemainingTime),
            _ => null
        };
    }

    private static string GetDebugState(CoroutineInstance instance)
    {
        if (instance.Fault != null)
        {
            return CoroutineDebugStates.Faulted;
        }

        if (instance.IsStopped)
        {
            return CoroutineDebugStates.Stopped;
        }

        if (instance.IsCompleted)
        {
            return CoroutineDebugStates.Completed;
        }

        if (instance.CurrentInstruction != null || instance.WaitingHandle.IsValid || instance.ResumeFrameIndex >= 0)
        {
            return CoroutineDebugStates.Waiting;
        }

        return CoroutineDebugStates.Running;
    }

    private static class CoroutineDebugStates
    {
        public const string Completed = "Completed";
        public const string Faulted = "Faulted";
        public const string Running = "Running";
        public const string Stopped = "Stopped";
        public const string Waiting = "Waiting";
    }
}