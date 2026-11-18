namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>How the transport of the cutscene preview advances the preview world.</summary>
public enum CutscenePreviewTransportMode
{
    /// <summary>The world advances by the time of each editor frame.</summary>
    Running,

    /// <summary>The world is frozen, except for the frame steps that were requested.</summary>
    Paused,

    /// <summary>The world advances by fixed steps, within a budget per frame, up to a target time.</summary>
    Seeking
}
