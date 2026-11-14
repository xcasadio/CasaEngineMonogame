using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Remote.Protocol;

namespace CasaEngine.Editor.Remote;

/// <summary>
/// Runs remote calls on the game thread (ADR-0092, plan P10). The pipe thread enqueues; <see cref="Pump"/>, called
/// once per editor frame, executes at most <see cref="MaxCallsPerPump"/> new calls and polls the deferred ones. No
/// exception of a command leaves <see cref="Pump"/>, and an idle pump allocates nothing.
/// </summary>
internal sealed class EditorRemoteDispatcher
{
    public const int MaxCallsPerPump = 4;

    private readonly EditorRemoteCommandRegistry _registry;
    private readonly ConcurrentQueue<EditorRemoteCall> _incoming = new();
    private readonly List<EditorRemoteCall> _deferred = new();
    private readonly Func<double> _clockSeconds;
    private readonly Action<string> _warn;

    public EditorRemoteDispatcher(EditorRemoteCommandRegistry registry, Func<double> clockSeconds = null, Action<string> warn = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _clockSeconds = clockSeconds ?? StopwatchSeconds;
        _warn = warn ?? Logs.WriteWarning;
    }

    public int DeferredCount => _deferred.Count;

    /// <summary>Thread-safe: called by the pipe thread.</summary>
    public void Enqueue(EditorRemoteCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        _incoming.Enqueue(call);
    }

    /// <summary>Game thread, once per frame.</summary>
    public void Pump()
    {
        if (_incoming.IsEmpty && _deferred.Count == 0)
        {
            return;
        }

        for (int i = 0; i < MaxCallsPerPump && _incoming.TryDequeue(out EditorRemoteCall call); i++)
        {
            Execute(call);
        }

        if (_deferred.Count == 0)
        {
            return;
        }

        double now = _clockSeconds();
        for (int i = _deferred.Count - 1; i >= 0; i--)
        {
            EditorRemoteCall call = _deferred[i];
            if (!call.IsCompleted)
            {
                Poll(call, now);
            }

            if (call.IsCompleted)
            {
                _deferred.RemoveAt(i);
            }
        }
    }

    /// <summary>Fails every call still waiting, for instance when the server stops.</summary>
    public void CancelAll(string message)
    {
        while (_incoming.TryDequeue(out EditorRemoteCall call))
        {
            call.Fail(RemoteError.Refused(RemoteErrorReasons.Failed, message));
        }

        for (int i = 0; i < _deferred.Count; i++)
        {
            _deferred[i].Fail(RemoteError.Refused(RemoteErrorReasons.Failed, message));
        }

        _deferred.Clear();
    }

    private void Execute(EditorRemoteCall call)
    {
        if (!_registry.TryGet(call.Method, out EditorRemoteCommand command))
        {
            call.Fail(new RemoteError { Code = RemoteErrorCodes.MethodNotFound, Message = $"Unknown command '{call.Method}'. Call '{RemoteProtocol.ListCommandsMethod}' for the list." });
            return;
        }

        try
        {
            command.Handler(call);
        }
        catch (Exception ex)
        {
            FailWith(call, ex);
        }

        if (call.IsCompleted)
        {
            return;
        }

        if (call.IsDeferred)
        {
            call.DeferredDeadline = _clockSeconds() + call.DeferredTimeoutSeconds;
            _deferred.Add(call);
            return;
        }

        call.Fail(new RemoteError { Code = RemoteErrorCodes.InternalError, Message = $"'{call.Method}' returned without an answer." });
    }

    private void Poll(EditorRemoteCall call, double now)
    {
        try
        {
            if (!call.DeferredPoll(call) && !call.IsCompleted && now >= call.DeferredDeadline)
            {
                call.Fail(RemoteError.Refused(RemoteErrorReasons.Timeout, $"'{call.Method}' did not finish in time."));
            }
        }
        catch (Exception ex)
        {
            FailWith(call, ex);
        }
    }

    private void FailWith(EditorRemoteCall call, Exception exception)
    {
        switch (exception)
        {
            case EditorRemoteArgumentException argument:
                call.Fail(new RemoteError { Code = RemoteErrorCodes.InvalidParams, Message = argument.Message, Data = new RemoteErrorData { Reason = RemoteErrorReasons.InvalidArgument } });
                break;
            case EditorRemoteRefusedException refused:
                call.Fail(RemoteError.Refused(refused.Reason, refused.Message));
                break;
            default:
                _warn($"[Remote] '{call.Method}' failed: {exception}");
                call.Fail(new RemoteError { Code = RemoteErrorCodes.InternalError, Message = $"'{call.Method}' failed: {exception.Message}" });
                break;
        }
    }

    private static double StopwatchSeconds() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
}
