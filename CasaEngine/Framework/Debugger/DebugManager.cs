using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Debugger;

public class DebugManager : Microsoft.Xna.Framework.DrawableGameComponent
{
    public Texture2D WhiteTexture { get; private set; }

    public DebugManager(Microsoft.Xna.Framework.Game game)
        : base(game)
    {
        // Added as a Service.
        //Game.Services.AddService(typeof(DebugManager), this);
        //Game.Components.Add(this);

        // This component doesn't need be call neither update nor draw.
        Enabled = false;
        Visible = false;


        /*UpdateOrder = (int)ComponentUpdateOrder.DebugManager;
         rawOrder = (int)ComponentDrawOrder.DebugManager;*/
    }

    protected override void LoadContent()
    {
        // Load debug content.
        //SpriteBatch = new SpriteBatch(GraphicsDevice);

        //DebugFont = Game.Content.Load<SpriteFont>(debugFont);

        // Create white texture.
        WhiteTexture = new Texture2D(GraphicsDevice, 1, 1);
        var whitePixels = new Color[] { Color.White };
        WhiteTexture.SetData(whitePixels);

        base.LoadContent();
    }
}