using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components.DebugTools
{
    public class DebugGridComponent : DrawableGameComponent
    {
        private VertexPositionColor[] LinesVertices;
        private int m_Size = 50;
        private Effect? GridEffect;
        private CasaEngineGame? _game;

        public DebugGridComponent(Game game) : base(game)
        {
            _game = game as CasaEngineGame;
            game.Components.Add(this);
        }

        protected override void Dispose(bool disposing)
        {
            // NOTE: Do NOT call Game.RemoveGameComponent<DebugGridComponent>() here.
            // That helper removes ALL components of type DebugGridComponent from the collection,
            // which would silently destroy every other per-view grid.
            // EngineHost.UnregisterEditorView calls game.Components.Remove(specificInstance)
            // before disposing the EditorViewContext.
            base.Dispose(disposing);
        }

        protected override void LoadContent()
        {
            int nbVertices = m_Size * 8 + 4;
            GridEffect = Game.Content.Load<Effect>("Shaders\\DebugPrimitiveColor").Clone();
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

            // Drawing is handled by OverlayViewPipeline.RenderGrid per-view.
            Visible = false;

            base.LoadContent();
        }

        public override void Draw(GameTime gameTime)
        {
            // Visible = false — this override is never called from DrawWithEditor.
            // Drawing is done by DrawForView() called from OverlayViewPipeline.
            base.Draw(gameTime);
        }

        /// <summary>
        /// Draws the grid for a specific view using the supplied camera frame.
        /// Called by <see cref="OverlayViewPipeline"/> with the view's render target active.
        /// </summary>
        public void DrawForView(GraphicsDevice gd, in RenderFrame frame)
        {
            if (GridEffect == null)
            {
                return;
            }

            gd.DepthStencilState = DepthStencilState.Default;
            gd.RasterizerState = RasterizerState.CullCounterClockwise;
            gd.BlendState = BlendState.Opaque;
            gd.Indices = null;
            gd.SetVertexBuffer(null);

            GridEffect.Parameters[ShaderParameterNames.WorldViewProj]?.SetValue(frame.View * frame.Projection);
            GridEffect.Parameters[ShaderParameterNames.ColorMultiplier]?.SetValue(Vector4.One);

            foreach (EffectPass pass in GridEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.LineList, LinesVertices, 0, LinesVertices.Length / 2);
            }
        }
    }
}

namespace CasaEngine.Framework.Application.Components.Editor
{
    [Obsolete("Use CasaEngine.Framework.Application.Components.DebugTools.DebugGridComponent instead.")]
    public sealed class GridComponent : DebugTools.DebugGridComponent
    {
        public GridComponent(Game game)
            : base(game)
        {
        }
    }
}