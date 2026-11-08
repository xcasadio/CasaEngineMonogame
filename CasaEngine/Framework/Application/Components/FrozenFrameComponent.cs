using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.FrozenFrames;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components;

/// <summary>
/// Holds the <see cref="FrozenFrameService"/> and draws the frozen image as a post-UI overlay
/// (E19.TR-4a, D-TR4A-5, D-TR4A-6).
/// </summary>
/// <remarks>SKELETON (E19.TR-4a, T1a): every member throws until T1b.</remarks>
public class FrozenFrameComponent : GameComponent, IPostUIOverlay
{
    public FrozenFrameComponent(Game game) : base(game) => throw new NotImplementedException();

    public FrozenFrameService Service => throw new NotImplementedException();

    internal FrameRetention Retention => throw new NotImplementedException();

    public override void Update(GameTime gameTime) => throw new NotImplementedException();

    public void Draw(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame) => throw new NotImplementedException();

    /// <summary>
    /// Pure seam: the destination rectangle of the base image over a view, or false when nothing is drawn
    /// (<paramref name="isBaseDrawable"/> false, or an empty view).
    /// </summary>
    internal static bool TryGetBaseDestination(bool isBaseDrawable, Rectangle viewportRect, out Rectangle destination)
        => throw new NotImplementedException();

    // Does not throw even in the skeleton: GameComponent's finalizer calls it, and a throw there kills the test host.
    protected override void Dispose(bool disposing) => base.Dispose(disposing);
}
