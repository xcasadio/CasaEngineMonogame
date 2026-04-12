using MGUI.Shared.Rendering.Clipping;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Clipping;

internal sealed class CasaScissorClipExecutor
{
    private readonly CasaDrawTransaction _owner;

    public CasaScissorClipExecutor(CasaDrawTransaction owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public ClipScope Push(ClipResolveResult resolution)
        => _owner.PushRectangleClipCore(resolution.Effective.Shape.Bounds, resolution.Effective.IntersectWithCurrentClip, resolution);
}