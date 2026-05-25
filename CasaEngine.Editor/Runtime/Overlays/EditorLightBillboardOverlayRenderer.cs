using System;
using System.Collections.Generic;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.Runtime.Overlays;

public sealed class EditorLightBillboardOverlayRenderer : IDisposable
{
    public const int IconSizePixels = 26;
    public const string PointIconName = "icons/png-white/lightbulb";
    public const string SpotIconName = "icons/png-white/cone";
    public const string DirectionalIconName = "icons/png-white/sun";

    private readonly SpriteBatch _spriteBatch;

    public EditorLightBillboardOverlayRenderer(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _spriteBatch = new SpriteBatch(graphicsDevice);
    }

    public static string GetIconName(LightType lightType)
    {
        return lightType switch
        {
            LightType.Spot => SpotIconName,
            LightType.Directional => DirectionalIconName,
            _ => PointIconName,
        };
    }

    public int LastDrawnItemCount { get; private set; }

    public void Draw(GraphicsDevice graphicsDevice, in RenderFrame frame, IReadOnlyList<EditorLightOverlayItem> lights)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(lights);

        LastDrawnItemCount = 0;

        if (lights.Count == 0)
        {
            return;
        }

        using var guard = new GraphicsStateGuard(graphicsDevice);
        var viewport = new Viewport(frame.ViewportRect);

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        for (int index = 0; index < lights.Count; index++)
        {
            if (DrawLightIcon(viewport, in frame, lights[index]))
            {
                LastDrawnItemCount++;
            }
        }

        _spriteBatch.End();
    }

    public void Dispose()
    {
        _spriteBatch.Dispose();
    }

    private bool DrawLightIcon(Viewport viewport, in RenderFrame frame, in EditorLightOverlayItem light)
    {
        Texture2D? texture = GetIconTexture(light.Type);
        if (texture == null)
        {
            return false;
        }

        Vector3 projected = viewport.Project(light.Position, frame.Projection, frame.View, Matrix.Identity);
        if (projected.Z < 0.0f || projected.Z > 1.0f)
        {
            return false;
        }

        if (projected.X < viewport.X || projected.X > viewport.X + viewport.Width
            || projected.Y < viewport.Y || projected.Y > viewport.Y + viewport.Height)
        {
            return false;
        }

        const int halfSize = IconSizePixels / 2;
        var destination = new Rectangle(
            (int)MathF.Round(projected.X) - halfSize,
            (int)MathF.Round(projected.Y) - halfSize,
            IconSizePixels,
            IconSizePixels);

        _spriteBatch.Draw(texture, destination, Color.White);
        return true;
    }

    private static Texture2D? GetIconTexture(LightType lightType)
    {
        return lightType switch
        {
            LightType.Spot => EditorIcons.Cone,
            LightType.Directional => EditorIcons.Sun,
            _ => EditorIcons.Lightbulb,
        };
    }
}