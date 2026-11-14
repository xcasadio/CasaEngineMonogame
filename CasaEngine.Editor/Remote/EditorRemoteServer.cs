using System;
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
/// The editor end of the pipe (ADR-0092, plan P1 and P10). One background task accepts one client at a time, reads
/// bounded lines, hands each request to the <see cref="EditorRemoteDispatcher"/> and writes the answer itself: the
/// game thread never waits on the pipe.
/// <para/>
/// The pipe is created as the first instance of its name (a squatted name fails), with an ACL that grants the
/// current user and denies network logons.
/// </summary>
internal sealed class EditorRemoteServer : IDisposable
{
    private readonly EditorRemoteDispatcher _dispatcher;
    private readonly TimeSpan _idleTimeout;
    private readonly TimeSpan _answerTimeout;
    private readonly CancellationTokenSource _stop = new();
    private NamedPipeServerStream _pipe;
    private Task _loop;

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

    public bool IsRunning => _loop != null && !_loop.IsCompleted;

    /// <summary>Creates the pipe. Throws <see cref="IOException"/> when the name already exists.</summary>
    public void Start()
    {
        if (_pipe != null)
        {
            throw new InvalidOperationException("The remote server is already started.");
        }

        _pipe = CreatePipe(PipeName);
        _loop = Task.Run(() => AcceptLoopAsync(_stop.Token));
    }

    public void Dispose()
    {
        _stop.Cancel();
        _pipe?.Dispose();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The loop ends by cancellation or by the disposed pipe.
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

    private static NamedPipeServerStream CreatePipe(string pipeName)
        => NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance,
            inBufferSize: 0,
            outBufferSize: 0,
            CreatePipeSecurity());

    private async Task AcceptLoopAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await _pipe.WaitForConnectionAsync(stop).ConfigureAwait(false);
                if (IsLocalClient(_pipe))
                {
                    await ServeClientAsync(stop).ConfigureAwait(false);
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
            catch (IOException ex)
            {
                _dispatcher.PostLog(LogVerbosity.Warning, $"[Remote] Client connection ended: {ex.Message}");
            }

            try
            {
                if (_pipe.IsConnected)
                {
                    _pipe.Disconnect();
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
            {
                return;
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

    private async Task ServeClientAsync(CancellationToken stop)
    {
        var reader = new RemoteLineReader(_pipe);
        while (!stop.IsCancellationRequested && _pipe.IsConnected)
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

            await RemoteMessageCodec.WriteAsync(_pipe, response, stop).ConfigureAwait(false);
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
        return RemoteResponse.Failure(request.Id, RemoteError.Refused(RemoteErrorReasons.Timeout, $"The editor did not answer '{request.Method}' in time."));
    }
}
