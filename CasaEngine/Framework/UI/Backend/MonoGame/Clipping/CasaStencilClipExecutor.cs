using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Clipping;

internal sealed class CasaStencilClipExecutor
{
    private readonly CasaDrawTransaction _owner;
    private int _stencilDepth;

    private const int MaxStencilDepth = 255;

    public CasaStencilClipExecutor(CasaDrawTransaction owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public int MaxObservedDepth { get; private set; }

    public ClipScope Push(ClipResolveResult resolution)
    {
        ClipGeometry geometry = resolution.Effective.Shape.Geometry ?? throw new InvalidOperationException(
            $"Clip '{resolution.Effective.DebugName ?? resolution.Effective.Kind.ToString()}' requires clip geometry for stencil rendering.");

        if (_stencilDepth == 0)
        {
            _owner.ClearStencil(0);
        }

        if (_stencilDepth >= MaxStencilDepth)
        {
            throw new InvalidOperationException($"Maximum stencil clip nesting depth of {MaxStencilDepth} was exceeded.");
        }

        int parentDepth = _stencilDepth;
        int childDepth = _stencilDepth + 1;
        MaxObservedDepth = Math.Max(MaxObservedDepth, childDepth);

        using (_owner.SetDrawSettingsTemporary(_owner.CurrentSettings with
        {
            BlendType = BlendType.ColorWriteDisable,
            DepthStencilType = DepthStencilType.StencilWriteIncrement,
            StencilReference = parentDepth,
        }))
        {
            _owner.DrawClipGeometry(geometry);
        }

        IDisposable stencilReadScope = _owner.SetDrawSettingsTemporary(_owner.CurrentSettings with
        {
            DepthStencilType = DepthStencilType.StencilReadEqual,
            StencilReference = childDepth,
        });
        _stencilDepth = childDepth;

        ClipResolveResult effectiveResolution = resolution with { StencilDepth = childDepth };

        return new ClipScope(effectiveResolution, () =>
        {
            stencilReadScope.Dispose();

            using (_owner.SetDrawSettingsTemporary(_owner.CurrentSettings with
            {
                BlendType = BlendType.ColorWriteDisable,
                DepthStencilType = DepthStencilType.StencilRestoreDecrement,
                StencilReference = childDepth,
            }))
            {
                _owner.DrawClipGeometry(geometry);
            }

            _stencilDepth = parentDepth;
        });
    }
}