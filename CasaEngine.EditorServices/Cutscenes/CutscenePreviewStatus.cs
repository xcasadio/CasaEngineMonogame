namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>What the transport row of a cutscene document shows (see <see cref="CutscenePreviewTransport.GetStatus"/>).</summary>
public enum CutscenePreviewStatus
{
    /// <summary>The preview is advancing to a requested time.</summary>
    Seeking,

    /// <summary>A play is running and frozen.</summary>
    Paused,

    /// <summary>A play is running.</summary>
    Playing,

    /// <summary>A play ended by itself: it completed, failed, or was stopped by a refused change of world.</summary>
    Ended,

    /// <summary>No play has run, or the preview was stopped.</summary>
    Stopped
}
