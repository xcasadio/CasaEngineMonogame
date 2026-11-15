using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Remote.Protocol;
using Microsoft.Win32.SafeHandles;

namespace CasaEngine.Editor.Remote;

/// <summary>
/// The editor end of the pipe (ADR-0092 P1 and P10, ADR-0093 P1). Up to <see cref="MaxClients"/> pipe instances each
/// serve one client at a time: an instance reads bounded lines, hands each request to the
/// <see cref="EditorRemoteDispatcher"/> and writes the answer itself, so the game thread never waits on the pipe, and a
/// slow or stuck client never hides the editor from the others.
/// <para/>
/// Every instance is created by <see cref="CreatePipe"/> with the same ACL (current user, network logons denied) and
/// the same instance limit. The first instance is created with <see cref="PipeOptions.FirstPipeInstance"/> (a squatted
/// name fails) and the others while it exists; instances are reused through <c>Disconnect</c>, never recreated, so the
/// name is never free while the server runs (security review S2-7).
/// </summary>
internal sealed class EditorRemoteServer : IDisposable
{
    /// <summary>Clients served at the same time.</summary>
    public const int MaxClients = 4;

    private readonly EditorRemoteDispatcher _dispatcher;
    private readonly TimeSpan _idleTimeout;
    private readonly TimeSpan _answerTimeout;
    private readonly CancellationTokenSource _stop = new();
    private readonly List<NamedPipeServerStream> _instances = new();
    private readonly List<Task> _loops = new();

    public EditorRemoteServer(EditorRemoteDispatcher dispatcher, string pipeName, TimeSpan idleTimeout, TimeSpan answerTimeout)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (!RemoteDiscovery.IsValidPipeName(pipeName))
        {
            throw new ArgumentException($"'{pipeName}' is not an editor pipe name.", nameof(pipeName));
        }

        _dispatcher = dispatcher;
        PipeName = pipeName;
        _idleTimeout = idleTimeout;
        _answerTimeout = answerTimeout;
    }

    public string PipeName { get; }

    /// <summary>True while at least one instance still accepts clients.</summary>
    public bool IsRunning
    {
        get
        {
            for (int i = 0; i < _loops.Count; i++)
            {
                if (!_loops[i].IsCompleted)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The pipe instances, for the tests that read their security.</summary>
    internal IReadOnlyList<NamedPipeServerStream> Instances => _instances;

    /// <summary>Creates every instance. Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> when the name already exists.</summary>
    public void Start()
    {
        if (_instances.Count != 0)
        {
            throw new InvalidOperationException("The remote server is already started.");
        }

        try
        {
            for (int i = 0; i < MaxClients; i++)
            {
                _instances.Add(CreatePipe(PipeName, firstInstance: i == 0));
            }
        }
        catch
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                _instances[i].Dispose();
            }

            _instances.Clear();
            throw;
        }

        for (int i = 0; i < _instances.Count; i++)
        {
            NamedPipeServerStream pipe = _instances[i];
            _loops.Add(Task.Run(() => RunAcceptLoopAsync(pipe, _stop.Token)));
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        for (int i = 0; i < _instances.Count; i++)
        {
            _instances[i].Dispose();
        }

        try
        {
            Task.WaitAll(_loops.ToArray(), TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The loops end by cancellation or by their disposed instance.
        }

        _stop.Dispose();
    }

    /// <summary>The pipe security of P1: full access for the current user, nothing for network logons.</summary>
    internal static PipeSecurity CreatePipeSecurity()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(identity.User, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        security.SetOwner(identity.User);
        return security;
    }

    /// <summary>The only way instances are made: same ACL, same instance limit; the first one must be the first of its name.</summary>
    private static NamedPipeServerStream CreatePipe(string pipeName, bool firstInstance)
        => NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            MaxClients,
            PipeTransmissionMode.Byte,
            firstInstance ? PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance : PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            CreatePipeSecurity());

    /// <summary>An accept loop never ends in silence: an unexpected error is logged (verifier finding A4).</summary>
    private async Task RunAcceptLoopAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        try
        {
            await AcceptLoopAsync(pipe, stop).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _dispatcher.PostLog(LogVerbosity.Error, $"[Remote] A remote control pipe instance stopped after an unexpected error: {ex}");
        }
    }

    private async Task AcceptLoopAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await pipe.WaitForConnectionAsync(stop).ConfigureAwait(false);
                if (IsLocalClient(pipe))
                {
                    await ServeClientAsync(pipe, stop).ConfigureAwait(false);
                }
                else
                {
                    _dispatcher.PostLog(LogVerbosity.Warning, "[Remote] Refused a client that came through the network redirector.");
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                _dispatcher.PostLog(LogVerbosity.Warning, $"[Remote] Client connection ended: {ex.Message}");
            }

            // Always disconnect: once the client has closed, the pipe can be Broken with IsConnected false, and waiting
            // for the next client without a Disconnect throws and would end this loop for good.
            try
            {
                pipe.Disconnect();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                // Not connected: nothing to release.
            }
            catch (IOException ex)
            {
                _dispatcher.PostLog(LogVerbosity.Warning, $"[Remote] Could not release the client connection: {ex.Message}");
            }
        }
    }

    private const int ErrorPipeLocal = 229;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientComputerName(SafePipeHandle pipe, char[] clientComputerName, uint clientComputerNameLength);

    /// <summary>
    /// True only for a client opened on this machine through <c>\\.\pipe\</c>. A client that came through the network
    /// redirector (another machine, but also <c>\\localhost</c>, <c>\\127.0.0.1</c> or the machine name) has a client
    /// computer name; Windows answers ERROR_PIPE_LOCAL for a local one. Any other answer is refused (security review
    /// S2: .NET cannot set PIPE_REJECT_REMOTE_CLIENTS, and a loopback redirector logon is not in the NETWORK group).
    /// </summary>
    internal static bool IsLocalClient(NamedPipeServerStream pipe)
    {
        var name = new char[256];
        if (GetNamedPipeClientComputerName(pipe.SafePipeHandle, name, (uint)(name.Length * sizeof(char))))
        {
            return false;
        }

        return Marshal.GetLastWin32Error() == ErrorPipeLocal;
    }

    private async Task ServeClientAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        var reader = new RemoteLineReader(pipe);
        while (!stop.IsCancellationRequested && pipe.IsConnected)
        {
            RemoteLine line;
            using (var idle = CancellationTokenSource.CreateLinkedTokenSource(stop))
            {
                idle.CancelAfter(_idleTimeout);
                try
                {
                    line = await reader.ReadLineAsync(idle.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!stop.IsCancellationRequested)
                {
                    _dispatcher.PostLog(LogVerbosity.Info, "[Remote] Idle client disconnected.");
                    return;
                }
            }

            if (line.Status == RemoteLineStatus.EndOfStream)
            {
                return;
            }

            RemoteResponse response;
            if (line.Status == RemoteLineStatus.TooLong)
            {
                response = RemoteResponse.Failure(null, new RemoteError
                {
                    Code = RemoteErrorCodes.InvalidRequest,
                    Message = $"A message is limited to {RemoteProtocol.MaxMessageBytes} bytes.",
                    Data = new RemoteErrorData { Reason = RemoteErrorReasons.MessageTooLong },
                });
            }
            else if (!RemoteMessageCodec.TryDecodeRequest(line.Text, out RemoteRequest request, out RemoteResponse error))
            {
                response = error;
            }
            else
            {
                response = await RunAsync(request, stop).ConfigureAwait(false);
            }

            byte[] encoded = RemoteMessageCodec.Encode(response);
            if (encoded.Length > RemoteProtocol.MaxMessageBytes + 1)
            {
                // The client refuses longer lines: say so instead of sending one (verifier finding A2).
                encoded = RemoteMessageCodec.Encode(RemoteResponse.Failure(response.Id, RemoteError.Refused(
                    RemoteErrorReasons.MessageTooLong,
                    $"The answer is longer than {RemoteProtocol.MaxMessageBytes} bytes: ask for less (for instance entity.get per entity).")));
            }

            await pipe.WriteAsync(encoded, stop).ConfigureAwait(false);
            await pipe.FlushAsync(stop).ConfigureAwait(false);
        }
    }

    private async Task<RemoteResponse> RunAsync(RemoteRequest request, CancellationToken stop)
    {
        var call = new EditorRemoteCall(request);
        _dispatcher.Enqueue(call);

        Task finished = await Task.WhenAny(call.Completion, Task.Delay(_answerTimeout, stop)).ConfigureAwait(false);
        if (finished == call.Completion)
        {
            return await call.Completion.ConfigureAwait(false);
        }

        stop.ThrowIfCancellationRequested();

        // Complete the call so the dispatcher drops it instead of running it after the client was told it timed out
        // (verifier finding A1). A handler already running on the game thread finishes, but its answer is ignored.
        call.Fail(RemoteError.Refused(RemoteErrorReasons.Timeout, $"The editor did not answer '{request.Method}' in time."));
        return await call.Completion.ConfigureAwait(false);
    }
}
