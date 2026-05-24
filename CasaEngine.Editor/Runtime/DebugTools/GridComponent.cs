using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components.DebugTools
{
    public class GridComponent : DrawableGameComponent
    {
        private VertexPositionColor[] _linesVertices;
        private const int Size = 50;
        private Effect _gridEffect;
        private CasaEngineGame _game;

        public GridComponent(Game game) : base(game)
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
            int nbVertices = Size * 8 + 4;
            _gridEffect = Game.Content.Load<Effect>("Shaders\\DebugPrimitiveColor").Clone();
            _linesVertices = new VertexPositionColor[nbVertices];
            int i = 0;

            for (int x = Size; x > 0; x--)
            {
                Color color;
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

                _linesVertices[i++] = new VertexPositionColor(new Vector3(x, 0.0f, Size), color);
                _linesVertices[i++] = new VertexPositionColor(new Vector3(x, 0.0f, -Size), color);

                _linesVertices[i++] = new VertexPositionColor(new Vector3(-x, 0.0f, Size), color);
                _linesVertices[i++] = new VertexPositionColor(new Vector3(-x, 0.0f, -Size), color);

                _linesVertices[i++] = new VertexPositionColor(new Vector3(Size, 0.0f, x), color);
                _linesVertices[i++] = new VertexPositionColor(new Vector3(-Size, 0.0f, x), color);

                _linesVertices[i++] = new VertexPositionColor(new Vector3(Size, 0.0f, -x), color);
                _linesVertices[i++] = new VertexPositionColor(new Vector3(-Size, 0.0f, -x), color);
            }

            _linesVertices[i++] = new VertexPositionColor(new Vector3(-Size, 0.0f, 0), Color.DarkBlue);
            _linesVertices[i++] = new VertexPositionColor(new Vector3(Size, 0.0f, 0), Color.DarkBlue);
            _linesVertices[i++] = new VertexPositionColor(new Vector3(0, 0.0f, Size), Color.DarkBlue);
            _linesVertices[i++] = new VertexPositionColor(new Vector3(0, 0.0f, -Size), Color.DarkBlue);

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
            if (_gridEffect == null)
            {
                return;
            }

            gd.DepthStencilState = DepthStencilState.Default;
            gd.RasterizerState = RasterizerState.CullCounterClockwise;
            gd.BlendState = BlendState.Opaque;
            gd.Indices = null;
            gd.SetVertexBuffer(null);

            _gridEffect.Parameters[ShaderParameterNames.WorldViewProj]?.SetValue(frame.View * frame.Projection);
            _gridEffect.Parameters[ShaderParameterNames.ColorMultiplier]?.SetValue(Vector4.One);

            foreach (EffectPass pass in _gridEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.LineList, _linesVertices, 0, _linesVertices.Length / 2);
            }
        }
    }
}
