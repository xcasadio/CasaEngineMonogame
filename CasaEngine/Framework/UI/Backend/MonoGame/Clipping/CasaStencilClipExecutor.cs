using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Clipping;

internal sealed class CasaStencilClipExecutor
{
    private readonly CasaDrawTransaction _owner;
    private int _stencilDepth;
    // The three settings a stencil clip derives at each depth, kept across the uses of the transaction (MGUI ADR-0031).
    private readonly DerivedDrawSettingsCache _derivedSettings = new();

    private const int MaxStencilDepth = 255;

    public CasaStencilClipExecutor(CasaDrawTransaction owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public int MaxObservedDepth { get; private set; }

    /// <summary>Puts the executor in the state its constructor gives it. A stencil clip left open by the previous use is forgotten: the next
    /// push clears the stencil buffer, as the first push of a new executor does.</summary>
    public void Rearm()
    {
        _stencilDepth = 0;
        MaxObservedDepth = 0;
    }

    public ClipHandle Push(ClipResolveResult resolution)
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

        using (_owner.SetDrawSettingsTemporary(_derivedSettings.WithStencil(_owner.CurrentSettings, BlendType.ColorWriteDisable,
                   DepthStencilType.StencilWriteIncrement, parentDepth)))
        {
            _owner.DrawClipGeometry(geometry);
        }

        DrawSettings current = _owner.CurrentSettings;
        DrawStateHandle stencilReadScope = _owner.SetDrawSettingsTemporary(_derivedSettings.WithStencil(current, current.BlendType,
            DepthStencilType.StencilReadEqual, childDepth));
        _stencilDepth = childDepth;

        ClipResolveResult effectiveResolution = resolution with { StencilDepth = childDepth };

        return _owner.ClipSlots.Push(ClipSlotState.Stencil(effectiveResolution, stencilReadScope, parentDepth, childDepth));
    }

    /// <summary>Ends the stencil clip of <paramref name="state"/>: back to the settings before it, its geometry taken off the stencil.</summary>
    public void Restore(in ClipSlotState state)
    {
        state.StencilReadScope.Dispose();

        using (_owner.SetDrawSettingsTemporary(_derivedSettings.WithStencil(_owner.CurrentSettings, BlendType.ColorWriteDisable,
                   DepthStencilType.StencilRestoreDecrement, state.ChildStencilDepth)))
        {
            _owner.DrawClipGeometry(state.Resolution.Effective.Shape.Geometry.Value);
        }

        _stencilDepth = state.ParentStencilDepth;
    }
}