using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Clipping
{
    internal sealed class CasaClipManager
    {
        private readonly CasaDrawTransaction _Owner;
        private readonly ClipBackendCapabilities _Capabilities;
        private int _ScissorClipCount;
        private int _StencilClipCount;
        private int _MaskClipCount;
        private int _MaxStencilDepth;
        private int _TemporaryRenderTargetRentCount;
        private int _TemporaryRenderTargetReuseCount;

        public CasaClipManager(CasaDrawTransaction owner)
        {
            _Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _Capabilities = ClipBackendCapabilities.Default;
        }

        public ClipResolveResult Resolve(ClipDefinition definition)
            => ClipStrategyResolver.Resolve(definition, _Capabilities);

        public ClipDiagnosticsSnapshot GetDiagnostics()
            => new(_ScissorClipCount, _StencilClipCount, _MaskClipCount, _MaxStencilDepth,
                _TemporaryRenderTargetRentCount, _TemporaryRenderTargetReuseCount);

        public ClipScope Push(ClipDefinition definition)
        {
            ClipResolveResult resolution = Resolve(definition);

            return resolution.Strategy switch
            {
                ClipStrategy.None => new(resolution, () => { }),
                ClipStrategy.Scissor => PushScissor(resolution),
                ClipStrategy.Stencil => PushStencil(resolution),
                ClipStrategy.Mask => PushMask(resolution),
                _ => throw new NotSupportedException($"Clip strategy '{resolution.Strategy}' is not available until the corresponding backend is installed.")
            };
        }

        private ClipScope PushScissor(ClipResolveResult resolution)
        {
            _ScissorClipCount++;
            return _Owner.PushRectangleClipCore(resolution.Effective.Shape.Bounds, resolution.Effective.IntersectWithCurrentClip, resolution);
        }

        private int _StencilDepth;
        private const int MaxStencilDepth = 255;

        private ClipScope PushStencil(ClipResolveResult resolution)
        {
            ClipGeometry geometry = resolution.Effective.Shape.Geometry ?? throw new InvalidOperationException(
                $"Clip '{resolution.Effective.DebugName ?? resolution.Effective.Kind.ToString()}' requires clip geometry for stencil rendering.");

            if (_StencilDepth == 0)
            {
                _Owner.ClearStencil(0);
            }

            if (_StencilDepth >= MaxStencilDepth)
            {
                throw new InvalidOperationException($"Maximum stencil clip nesting depth of {MaxStencilDepth} was exceeded.");
            }

            int parentDepth = _StencilDepth;
            int childDepth = _StencilDepth + 1;
            _StencilClipCount++;
            _MaxStencilDepth = Math.Max(_MaxStencilDepth, childDepth);

            using (_Owner.SetDrawSettingsTemporary(_Owner.CurrentSettings with
            {
                BlendType = BlendType.ColorWriteDisable,
                DepthStencilType = DepthStencilType.StencilWriteIncrement,
                StencilReference = parentDepth,
            }))
            {
                _Owner.DrawClipGeometry(geometry);
            }

            IDisposable stencilReadScope = _Owner.SetDrawSettingsTemporary(_Owner.CurrentSettings with
            {
                DepthStencilType = DepthStencilType.StencilReadEqual,
                StencilReference = childDepth,
            });
            _StencilDepth = childDepth;

            ClipResolveResult effectiveResolution = resolution with { StencilDepth = childDepth };

            return new ClipScope(effectiveResolution, () =>
            {
                stencilReadScope.Dispose();

                using (_Owner.SetDrawSettingsTemporary(_Owner.CurrentSettings with
                {
                    BlendType = BlendType.ColorWriteDisable,
                    DepthStencilType = DepthStencilType.StencilRestoreDecrement,
                    StencilReference = childDepth,
                }))
                {
                    _Owner.DrawClipGeometry(geometry);
                }

                _StencilDepth = parentDepth;
            });
        }

        private ClipScope PushMask(ClipResolveResult resolution)
        {
            ClipGeometry geometry = resolution.Effective.Shape.Geometry ?? throw new InvalidOperationException(
                $"Clip '{resolution.Effective.DebugName ?? resolution.Effective.Kind.ToString()}' requires clip geometry for mask rendering.");
            // Bounds are always expressed in the current render-target space. Geometry stays in local draw space and
            // is shifted into the temporary target through the active transform so RenderScale and parent transforms stay aligned.
            Rectangle bounds = resolution.Effective.Shape.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return new ClipScope(resolution, () => { });
            }

            CasaRenderTargetLease renderTargetLease = _Owner.Renderer.RenderTargetPool.Rent(_Owner.GraphicsDevice, bounds.Width, bounds.Height, false);
            RenderTarget2D maskTarget = renderTargetLease.Target;
            _MaskClipCount++;
            _TemporaryRenderTargetRentCount++;
            if (renderTargetLease.WasReused)
            {
                _TemporaryRenderTargetReuseCount++;
            }

            IDisposable depthStencilDisableScope = _Owner.SetDrawSettingsTemporary(_Owner.CurrentSettings with
            {
                DepthStencilType = DepthStencilType.None,
            });
            IDisposable renderTargetScope = _Owner.SetRenderTargetTemporary(maskTarget, Color.Transparent);
            ClipScope clipDisableScope = _Owner.PushRectangleClip(null, false);
            IDisposable transformScope = _Owner.SetTransformTemporary(_Owner.CurrentSettings.Transform * Matrix.CreateTranslation(-bounds.Left, -bounds.Top, 0));

            using (_Owner.SetDrawSettingsTemporary(_Owner.CurrentSettings with
            {
                BlendType = BlendType.Opaque,
                DepthStencilType = DepthStencilType.None,
            }))
            {
                for (int i = 0; i + 2 < geometry.Indices.Count; i += 3)
                {
                    Vector2 v0 = geometry.Vertices[geometry.Indices[i]];
                    Vector2 v1 = geometry.Vertices[geometry.Indices[i + 1]];
                    Vector2 v2 = geometry.Vertices[geometry.Indices[i + 2]];
                    _Owner.FillTriangle(Vector2.Zero, v0, Color.Black, v1, Color.Black, v2, Color.Black);
                }
            }

            IDisposable maskedContentScope = _Owner.SetDrawSettingsTemporary(_Owner.CurrentSettings with
            {
                BlendType = BlendType.DestinationAlphaMask,
                DepthStencilType = DepthStencilType.None,
            });

            return new ClipScope(resolution, () =>
            {
                maskedContentScope.Dispose();
                transformScope.Dispose();
                renderTargetScope.Dispose();
                clipDisableScope.Dispose();
                depthStencilDisableScope.Dispose();

                _Owner.DrawTextureTo(maskTarget, null, bounds);
                _Owner.Renderer.RenderTargetPool.Return(maskTarget);
            });
        }
    }
}
