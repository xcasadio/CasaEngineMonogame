namespace CasaEngine.Framework.Rendering.FrozenFrames;

/// <summary>Outcome of the last capture requested from <see cref="FrozenFrameService"/>.</summary>
public enum FrozenFrameCaptureState
{
    /// <summary>No capture was ever completed or failed.</summary>
    None,

    /// <summary>The last request kept the image of the previous frame.</summary>
    Captured,

    /// <summary>The last request found no retained image (first frame, resize, ineligible view, option off).</summary>
    Unavailable,
}
