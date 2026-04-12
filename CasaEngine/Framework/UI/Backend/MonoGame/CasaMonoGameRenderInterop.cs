using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Rendering;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

internal static class CasaMonoGameRenderInterop
{
    private static readonly Dictionary<RasterizerType, RasterizerState> RasterizerMap = new()
    {
        { RasterizerType.Default, new RasterizerState { CullMode = CullMode.None } },
        { RasterizerType.SolidScissorTest, new RasterizerState { FillMode = FillMode.Solid, ScissorTestEnable = true, CullMode = CullMode.None } },
        { RasterizerType.Solid, new RasterizerState { FillMode = FillMode.Solid, ScissorTestEnable = false, CullMode = CullMode.None } },
        { RasterizerType.WireframeScissorTest, new RasterizerState { FillMode = FillMode.WireFrame, ScissorTestEnable = true, CullMode = CullMode.None } },
        { RasterizerType.Wireframe, new RasterizerState { FillMode = FillMode.WireFrame, ScissorTestEnable = false, CullMode = CullMode.None } },
    };

    private static readonly Dictionary<BlendType, BlendState> BlendMap = new()
    {
        { BlendType.Default, BlendState.AlphaBlend },
        { BlendType.Additive, BlendState.Additive },
        { BlendType.AlphaBlend, BlendState.AlphaBlend },
        { BlendType.NonPremultiplied, BlendState.NonPremultiplied },
        { BlendType.Opaque, BlendState.Opaque },
        { BlendType.ColorWriteDisable, new BlendState { ColorWriteChannels = ColorWriteChannels.None, ColorWriteChannels1 = ColorWriteChannels.None, ColorWriteChannels2 = ColorWriteChannels.None, ColorWriteChannels3 = ColorWriteChannels.None } },
        {
            BlendType.DestinationAlphaMask,
            new BlendState
            {
                ColorSourceBlend = Blend.DestinationAlpha,
                ColorDestinationBlend = Blend.InverseSourceAlpha,
                ColorBlendFunction = BlendFunction.Add,
                AlphaSourceBlend = Blend.DestinationAlpha,
                AlphaDestinationBlend = Blend.InverseSourceAlpha,
                AlphaBlendFunction = BlendFunction.Add,
            }
        },
    };

    private static readonly Dictionary<SamplerType, SamplerState> SamplerMap = new()
    {
        { SamplerType.Default, SamplerState.LinearClamp },
        { SamplerType.AnisotropicClamp, SamplerState.AnisotropicClamp },
        { SamplerType.AnisotropicWrap, SamplerState.AnisotropicWrap },
        { SamplerType.LinearClamp, SamplerState.LinearClamp },
        { SamplerType.LinearWrap, SamplerState.LinearWrap },
        { SamplerType.PointClamp, SamplerState.PointClamp },
        { SamplerType.PointWrap, SamplerState.PointWrap },
    };

    private static readonly Dictionary<DepthStencilType, DepthStencilState> DepthStencilMap = new()
    {
        { DepthStencilType.Default, DepthStencilState.None },
        { DepthStencilType.DepthRead, DepthStencilState.DepthRead },
        { DepthStencilType.None, DepthStencilState.None },
    };

    private static readonly Dictionary<(DepthStencilType Type, int Reference, int ReadMask, int WriteMask), DepthStencilState> CustomDepthStencilMap = new();

    internal static RasterizerState GetRasterizerState(DrawSettings settings) => RasterizerMap[settings.RasterizerType];
    internal static BlendState GetBlendState(DrawSettings settings) => BlendMap[settings.BlendType];
    internal static SamplerState GetSamplerState(DrawSettings settings) => SamplerMap[settings.SamplerType];

    internal static SpriteSortMode GetSortMode(DrawSettings settings)
        => settings.Sort switch
        {
            DrawSortMode.Deferred => SpriteSortMode.Deferred,
            DrawSortMode.Immediate => SpriteSortMode.Immediate,
            DrawSortMode.Texture => SpriteSortMode.Texture,
            DrawSortMode.BackToFront => SpriteSortMode.BackToFront,
            DrawSortMode.FrontToBack => SpriteSortMode.FrontToBack,
            _ => throw new NotImplementedException($"Unrecognized {nameof(DrawSortMode)}: {settings.Sort}"),
        };

    internal static DepthStencilState GetDepthStencilState(DrawSettings settings)
    {
        if (DepthStencilMap.TryGetValue(settings.DepthStencilType, out DepthStencilState existing))
        {
            return existing;
        }

        var key = (settings.DepthStencilType, settings.StencilReference, settings.StencilReadMask, settings.StencilWriteMask);
        if (!CustomDepthStencilMap.TryGetValue(key, out DepthStencilState result))
        {
            result = settings.DepthStencilType switch
            {
                DepthStencilType.StencilWriteIncrement => new DepthStencilState
                {
                    StencilEnable = true,
                    ReferenceStencil = settings.StencilReference,
                    StencilMask = settings.StencilReadMask,
                    StencilWriteMask = settings.StencilWriteMask,
                    StencilFunction = CompareFunction.Equal,
                    StencilPass = StencilOperation.Increment,
                    StencilFail = StencilOperation.Keep,
                    StencilDepthBufferFail = StencilOperation.Keep,
                    DepthBufferEnable = false,
                },
                DepthStencilType.StencilReadEqual => new DepthStencilState
                {
                    StencilEnable = true,
                    ReferenceStencil = settings.StencilReference,
                    StencilMask = settings.StencilReadMask,
                    StencilWriteMask = settings.StencilWriteMask,
                    StencilFunction = CompareFunction.Equal,
                    StencilPass = StencilOperation.Keep,
                    StencilFail = StencilOperation.Keep,
                    StencilDepthBufferFail = StencilOperation.Keep,
                    DepthBufferEnable = false,
                },
                DepthStencilType.StencilRestoreDecrement => new DepthStencilState
                {
                    StencilEnable = true,
                    ReferenceStencil = settings.StencilReference,
                    StencilMask = settings.StencilReadMask,
                    StencilWriteMask = settings.StencilWriteMask,
                    StencilFunction = CompareFunction.Equal,
                    StencilPass = StencilOperation.Decrement,
                    StencilFail = StencilOperation.Keep,
                    StencilDepthBufferFail = StencilOperation.Keep,
                    DepthBufferEnable = false,
                },
                _ => throw new NotImplementedException($"Unrecognized {nameof(DepthStencilType)}: {settings.DepthStencilType}"),
            };

            CustomDepthStencilMap[key] = result;
        }

        return result;
    }

    internal static Effect? GetEffect(DrawSettings settings) => settings.BackendEffect as Effect;

    internal static SpriteEffects ToSpriteEffects(UIDrawFlip flip)
    {
        SpriteEffects result = SpriteEffects.None;
        if ((flip & UIDrawFlip.Horizontal) != 0)
        {
            result |= SpriteEffects.FlipHorizontally;
        }

        if ((flip & UIDrawFlip.Vertical) != 0)
        {
            result |= SpriteEffects.FlipVertically;
        }

        return result;
    }
}