using CasaEngine.Framework.Rendering.EffectQuads;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components;

/// <summary>Stub of step G2e-1 (tests first): every behaviour throws.</summary>
public class EffectQuadComponent : GameComponent
{
    public EffectQuadComponent(Game game) : base(game)
    {
        Service = new EffectQuadService();
    }

    public EffectQuadService Service { get; }

    public override void Update(GameTime gameTime) => throw new System.NotImplementedException();

    public void ResolveTextures(System.Func<System.Guid, Texture2D> loader) => throw new System.NotImplementedException();

    public void Submit(SpriteRendererComponent renderer, Rectangle scissorRectangle) => throw new System.NotImplementedException();
}
