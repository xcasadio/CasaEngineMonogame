using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components.Editor;

public class GridComponent : DrawableGameComponent
{
    private VertexPositionColor[] LinesVertices;
    private int m_Size = 50;
    private BasicEffect GridEffect;
    private CasaEngineGame? _game;

    public GridComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        _game = game as CasaEngineGame;
        game.Components.Add(this);
    }

    protected override void Dispose(bool disposing)
    {
        // NOTE: Do NOT call Game.RemoveGameComponent<GridComponent>() here.
        // That helper removes ALL components of type GridComponent from the collection,
        // which would silently destroy every other per-view grid.
        // EngineHost.UnregisterEditorView calls game.Components.Remove(specificInstance)
        // before disposing the EditorViewContext.
        base.Dispose(disposing);
    }

    protected override void LoadContent()
    {
        int nbVertices = m_Size * 8 + 4;
        GridEffect = new BasicEffect(GraphicsDevice);
        GridEffect.VertexColorEnabled = true;
        GridEffect.LightingEnabled = false;
        LinesVertices = new VertexPositionColor[nbVertices];
        Color color;
        int i = 0;

        for (int x = m_Size; x > 0; x--)
        {
            if (x % 10 == 0)
            {
                color = Color.DarkBlue;
            }
            else if (x % 5 == 0)
            {
                color = Color.DarkGray;
            }
            else
            {
                color = Color.DimGray;
            }

            LinesVertices[i++] = new VertexPositionColor(new Vector3(x, 0.0f, m_Size), color);
            LinesVertices[i++] = new VertexPositionColor(new Vector3(x, 0.0f, -m_Size), color);

            LinesVertices[i++] = new VertexPositionColor(new Vector3(-x, 0.0f, m_Size), color);
            LinesVertices[i++] = new VertexPositionColor(new Vector3(-x, 0.0f, -m_Size), color);

            LinesVertices[i++] = new VertexPositionColor(new Vector3(m_Size, 0.0f, x), color);
            LinesVertices[i++] = new VertexPositionColor(new Vector3(-m_Size, 0.0f, x), color);

            LinesVertices[i++] = new VertexPositionColor(new Vector3(m_Size, 0.0f, -x), color);
            LinesVertices[i++] = new VertexPositionColor(new Vector3(-m_Size, 0.0f, -x), color);
        }

        LinesVertices[i++] = new VertexPositionColor(new Vector3(-m_Size, 0.0f, 0), Color.DarkBlue);
        LinesVertices[i++] = new VertexPositionColor(new Vector3(m_Size, 0.0f, 0), Color.DarkBlue);
        LinesVertices[i++] = new VertexPositionColor(new Vector3(0, 0.0f, m_Size), Color.DarkBlue);
        LinesVertices[i++] = new VertexPositionColor(new Vector3(0, 0.0f, -m_Size), Color.DarkBlue);

        // Drawing is handled by EditorViewPipeline.RenderGrid per-view.
        Visible = false;

        base.LoadContent();
    }

    public override void Draw(GameTime gameTime)
    {
        // Visible = false — this override is never called from DrawWithEditor.
        // Drawing is done by DrawForView() called from EditorViewPipeline.
        base.Draw(gameTime);
    }

    /// <summary>
    /// Draws the grid for a specific view using the supplied camera frame.
    /// Called by <see cref="EditorViewPipeline"/> with the view's render target active.
    /// </summary>
    public void DrawForView(GraphicsDevice gd, in RenderFrame frame)
    {
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullCounterClockwise;
        gd.BlendState = BlendState.Opaque;
        gd.Indices = null;
        gd.SetVertexBuffer(null);

        GridEffect.World      = Matrix.Identity;
        GridEffect.View       = frame.View;
        GridEffect.Projection = frame.Projection;

        foreach (EffectPass pass in GridEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawUserPrimitives(PrimitiveType.LineList, LinesVertices, 0, LinesVertices.Length / 2);
        }
    }
}