using System;
using System.IO;
using CasaEngine.Editor.Remote.Protocol;

namespace CasaEngine.Editor.Remote;

/// <summary>Result of a capture taken by the editor at the end of a frame.</summary>
internal sealed class RemoteCaptureResult
{
    public bool Succeeded { get; init; }

    public string Error { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }
}

/// <summary>Capture and play mode commands (ADR-0092, plan T3.2).</summary>
internal static class EditorRemoteViewCommands
{
    public const string WindowCapture = "window";
    public const string ViewportCapture = "viewport";
    public const double CaptureTimeoutSeconds = 30;

    public static void Register(EditorRemoteCommandRegistry registry, IEditorRemoteHost host, Func<string> newCapturePath)
    {
        registry.Register(new EditorRemoteCommand(
            "capture.window",
            "Captures the whole editor window to a PNG file after the next frame. Returns its path and size. The image leaves the machine with the model's requests.",
            null,
            readOnly: true,
            destructive: false,
            call => Capture(call, host, WindowCapture, newCapturePath())));

        registry.Register(new EditorRemoteCommand(
            "capture.viewport",
            "Captures the world viewport (the 3D or 2D view, without the panels) to a PNG file after the next frame. Returns its path and size.",
            null,
            readOnly: true,
            destructive: false,
            call => Capture(call, host, ViewportCapture, newCapturePath())));

        registry.Register(new EditorRemoteCommand(
            "play.status",
            "Play mode state: editing, starting, playing, paused or stopping.",
            null,
            readOnly: true,
            destructive: false,
            call => call.Succeed(new { state = host.PlayState })));

        registry.Register(new EditorRemoteCommand(
            "play.start",
            "Builds the gameplay scripts if needed and starts play mode: runs the project's code. Fails, with the reason in logs.read, when it does not start.",
            null,
            readOnly: false,
            destructive: true,
            call =>
            {
                if (!host.IsPlaySessionActive)
                {
                    host.StartPlay();
                    if (!host.IsPlaySessionActive)
                    {
                        throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, "Play mode did not start: see logs.read for the reason.");
                    }
                }

                call.Succeed(new { state = host.PlayState });
            }));

        registry.Register(new EditorRemoteCommand(
            "play.stop",
            "Stops play mode and returns to the edited world.",
            null,
            readOnly: false,
            destructive: false,
            call =>
            {
                if (host.IsPlaySessionActive)
                {
                    host.StopPlay();
                }

                call.Succeed(new { state = host.PlayState });
            }));
    }

    private static void Capture(EditorRemoteCall call, IEditorRemoteHost host, string kind, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!host.RequestCapture(kind, path, out string error))
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? "The capture could not be requested.");
        }

        call.Defer(
            c =>
            {
                if (!host.TryTakeCaptureResult(path, out RemoteCaptureResult result))
                {
                    return false;
                }

                if (result.Succeeded)
                {
                    c.Succeed(new { file = path, width = result.Width, height = result.Height, kind });
                }
                else
                {
                    c.Fail(RemoteError.Refused(RemoteErrorReasons.Failed, result.Error ?? "The capture failed."));
                }

                return true;
            },
            CaptureTimeoutSeconds);
    }
}
