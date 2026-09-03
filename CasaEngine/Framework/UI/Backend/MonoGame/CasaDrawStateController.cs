using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

internal sealed class CasaDrawStateController
{
    private readonly CasaDesktopRuntime _renderer;
    private readonly Matrix _primitiveProjectionMatrix;

    public CasaDrawStateController(CasaDesktopRuntime renderer, DrawSettings initialSettings, Matrix primitiveProjectionMatrix)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(initialSettings);

        _renderer = renderer;
        _primitiveProjectionMatrix = primitiveProjectionMatrix;
        CurrentSettings = initialSettings;
    }

    public DrawContext CurrentContext { get; private set; } = DrawContext.None;
    public DrawSettings CurrentSettings { get; private set; }
    public DrawSettings PreviousSettings { get; private set; } = DrawSettings.Default;

    public RasterizerState CurrentRasterizerState => CasaMonoGameRenderInterop.GetRasterizerState(CurrentSettings);
    public BlendState CurrentBlendState => CasaMonoGameRenderInterop.GetBlendState(CurrentSettings);
    public SamplerState CurrentSamplerState => CasaMonoGameRenderInterop.GetSamplerState(CurrentSettings);
    public DepthStencilState CurrentDepthStencilState => CasaMonoGameRenderInterop.GetDepthStencilState(CurrentSettings);

    public void Begin(DrawContext context)
    {
        if (context == DrawContext.None)
        {
            End(CurrentContext);
            return;
        }

        if (CurrentContext == context)
        {
            return;
        }

        if (CurrentContext != DrawContext.None)
        {
            End(CurrentContext);
        }

        switch (context)
        {
            case DrawContext.Sprites:
                _renderer.SpriteBatch.Begin(
                    CasaMonoGameRenderInterop.GetSortMode(CurrentSettings),
                    CurrentBlendState,
                    CurrentSamplerState,
                    CurrentDepthStencilState,
                    CurrentRasterizerState,
                    CasaMonoGameRenderInterop.GetEffect(CurrentSettings),
                    CurrentSettings.Transform);
                break;

            case DrawContext.Primitives:
                ApplyPrimitiveDeviceStates();
                Matrix primitiveProjectionMatrix = _primitiveProjectionMatrix;
                Matrix primitiveViewMatrix = CurrentSettings.Transform;
                _renderer.PrimitiveBatch.Begin(ref primitiveProjectionMatrix, ref primitiveViewMatrix);
                break;

            default:
                throw new NotImplementedException($"Unrecognized {nameof(DrawContext)}: {context}");
        }

        CurrentContext = context;
    }

    public void End(DrawContext context)
    {
        if (context == DrawContext.None || CurrentContext != context)
        {
            return;
        }

        switch (CurrentContext)
        {
            case DrawContext.Sprites:
                _renderer.SpriteBatch.End();
                break;

            case DrawContext.Primitives:
                _renderer.PrimitiveBatch.End();
                break;

            default:
                throw new NotImplementedException($"Unrecognized {nameof(DrawContext)}: {CurrentContext}");
        }

        CurrentContext = DrawContext.None;
    }

    public void SetDrawSettings(DrawSettings settings)
    {
        if (settings == null || settings == CurrentSettings)
        {
            return;
        }

        PreviousSettings = CurrentSettings;
        CurrentSettings = settings;
        End(CurrentContext);
    }

    public IDisposable SetDrawSettingsTemporary(DrawSettings settings)
        => new TemporaryChange<DrawSettings>(CurrentSettings, settings, SetDrawSettings);

    /// <summary>Pushes the primitive-context device states onto the <see cref="GraphicsDevice"/> without starting a batch.<para/>
    /// Used both by <see cref="Begin"/> and by draw calls that issue their own GPU call outside of the primitive batch.</summary>
    public void ApplyPrimitiveDeviceStates()
    {
        GraphicsDevice graphicsDevice = _renderer.GraphicsDevice;
        graphicsDevice.BlendState = CurrentBlendState;
        graphicsDevice.DepthStencilState = CurrentDepthStencilState;
        graphicsDevice.RasterizerState = CurrentRasterizerState;
        graphicsDevice.SamplerStates[0] = CurrentSamplerState;
    }
}