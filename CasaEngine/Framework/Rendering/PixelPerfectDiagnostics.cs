using CasaEngine.Core.Logging;
using CasaEngine.Framework.Scene.Entities.Components;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Reasons why the pixel-perfect contract of a view is not honoured.
/// </summary>
[Flags]
public enum PixelPerfectDegradation
{
    /// <summary>The view honours the pixel-perfect contract (or the contract does not apply).</summary>
    None = 0,

    /// <summary>The view renders at a resolution scale different from 1: texels do not map to pixels.</summary>
    ResolutionScale = 1 << 0,

    /// <summary>The camera zoom is not an integer factor: texels are resampled unevenly.</summary>
    NonIntegerZoom = 1 << 1,
}

/// <summary>
/// Pure evaluation of the pixel-perfect contract of a view: a <see cref="Camera2dComponent"/> with
/// <see cref="Camera2dComponent.PixelSnap"/> enabled only produces a texel-aligned image when the view
/// renders at scale 1 with an integer zoom.
/// </summary>
public static class PixelPerfectDiagnostics
{
    private const float Epsilon = 1e-4f;

    private const string ReasonResolutionScale = "ResolutionScale != 1";
    private const string ReasonNonIntegerZoom = "Zoom is not an integer";
    private const string ReasonBoth = "ResolutionScale != 1, Zoom is not an integer";
    private const string ReasonNone = "none";

    /// <summary>
    /// True when <paramref name="camera"/> asks for a pixel-perfect image, i.e. it is a
    /// <see cref="Camera2dComponent"/> with <see cref="Camera2dComponent.PixelSnap"/> enabled.
    /// </summary>
    public static bool RequiresPixelPerfect(CameraComponent camera)
    {
        return camera is Camera2dComponent { PixelSnap: true };
    }

    /// <summary>
    /// Evaluates the degradation of a pixel-snapped camera rendered with the given resolution scale.
    /// </summary>
    public static PixelPerfectDegradation Evaluate(float zoom, float resolutionScale)
    {
        var degradation = PixelPerfectDegradation.None;

        if (MathF.Abs(resolutionScale - 1.0f) > Epsilon)
        {
            degradation |= PixelPerfectDegradation.ResolutionScale;
        }

        if (MathF.Abs(zoom - MathF.Round(zoom)) > Epsilon)
        {
            degradation |= PixelPerfectDegradation.NonIntegerZoom;
        }

        return degradation;
    }

    /// <summary>
    /// Evaluates the degradation of a view. Returns <see cref="PixelPerfectDegradation.None"/> when the
    /// camera does not request a pixel-perfect image.
    /// </summary>
    public static PixelPerfectDegradation Evaluate(CameraComponent camera, float resolutionScale)
    {
        if (camera is not Camera2dComponent { PixelSnap: true } camera2d)
        {
            return PixelPerfectDegradation.None;
        }

        return Evaluate(camera2d.Zoom, resolutionScale);
    }

    /// <summary>Returns a cached description of the degradation reasons (no allocation).</summary>
    public static string DescribeReason(PixelPerfectDegradation degradation)
    {
        return degradation switch
        {
            PixelPerfectDegradation.ResolutionScale => ReasonResolutionScale,
            PixelPerfectDegradation.NonIntegerZoom => ReasonNonIntegerZoom,
            PixelPerfectDegradation.ResolutionScale | PixelPerfectDegradation.NonIntegerZoom => ReasonBoth,
            _ => ReasonNone,
        };
    }

    /// <summary>
    /// Logs a warning the first time <paramref name="view"/> is found to break its pixel-perfect contract.
    /// Subsequent calls for the same view are no-ops, so this is safe to call every frame.
    /// </summary>
    internal static void WarnOnce(RenderView view)
    {
        if (view.PixelPerfectWarningLogged)
        {
            return;
        }

        var degradation = Evaluate(view.Camera, view.ResolutionScale);
        if (degradation == PixelPerfectDegradation.None)
        {
            return;
        }

        view.PixelPerfectWarningLogged = true;

        var viewName = string.IsNullOrEmpty(view.Name) ? "unnamed" : view.Name;
        Logs.WriteWarning(
            $"[PixelPerfect] View '{viewName}': pixel snap is enabled but the pixel-perfect contract is degraded ({DescribeReason(degradation)}).");
    }
}
