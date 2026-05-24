using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components.DebugTools
{
    public class AxisComponent : DrawableGameComponent, IGameComponentResizable
    {
        private readonly CasaEngineGame _game;
        private VertexBuffer _vertexBuffer;
        private Effect _effect;
        private int _width;
        private int _height;

        public AxisComponent(Game game) : base(game)
        {
            _game = game as CasaEngineGame;
            game.Components.Add(this);
            UpdateOrder = (int)ComponentUpdateOrder.Manipulator;
            DrawOrder = (int)ComponentDrawOrder.Manipulator;
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            _effect = Game.Content.Load<Effect>("Shaders\\DebugPrimitiveColor").Clone();

            _vertexBuffer = new VertexBuffer(GraphicsDevice, VertexPositionColor.VertexDeclaration, 6, BufferUsage.None);
            _vertexBuffer.SetData(new VertexPositionColor[]
            {
                new(Vector3.Zero, Color.Red), new(Vector3.UnitX, Color.Red),
                new(Vector3.Zero, Color.Green), new(Vector3.UnitY, Color.Green),
                new(Vector3.Zero, Color.Blue), new(Vector3.UnitZ, Color.Blue)
            });

            _width = _game.ScreenSizeWidth;
            _height = _game.ScreenSizeHeight;

            // Drawing is handled by OverlayViewPipeline.RenderAxis per-view.
            Visible = false;
        }

        /// <summary>
        /// Draws the axis orientation indicator for a specific view.
        /// Uses <paramref name="frame"/>.ViewportRect for the pixel dimensions so the
        /// indicator is always scaled correctly regardless of the viewport size.
        /// Called by <see cref="OverlayViewPipeline"/> with the view's render target active.
        /// </summary>
        public void DrawForView(GraphicsDevice graphicsDevice, in RenderFrame frame)
        {
            if (_effect == null || _vertexBuffer == null)
            {
                return;
            }

            int width  = frame.ViewportRect.Width;
            int height = frame.ViewportRect.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            graphicsDevice.DepthStencilState = DepthStencilState.None;
            graphicsDevice.RasterizerState   = RasterizerState.CullNone;
            graphicsDevice.BlendState        = BlendState.Opaque;
            graphicsDevice.SetVertexBuffer(_vertexBuffer);
            graphicsDevice.Indices = null;

            //TODO : compute with screen height/width and aspect ratio
            var forwardFactor = (float)width  / 800f * 20f;
            var leftFactor    = (float)width  / 800f * 13f;
            var upFactor      = (float)height / 480f *  6f;

            var viewMatrix = Matrix.Invert(frame.View);
            var position   = viewMatrix.Translation
                           + viewMatrix.Forward * forwardFactor
                           + viewMatrix.Left    * leftFactor
                           - viewMatrix.Up      * upFactor;
            var world = MatrixExtensions.Transformation(Vector3.One, Quaternion.Identity, position);
            _effect.Parameters[ShaderParameterNames.WorldViewProj]?.SetValue(world * frame.View * frame.Projection);
            _effect.Parameters[ShaderParameterNames.ColorMultiplier]?.SetValue(Vector4.One);

            for (var i = 0; i < _effect.CurrentTechnique.Passes.Count; i++)
            {
                _effect.CurrentTechnique.Passes[i].Apply();
                graphicsDevice.DrawPrimitives(PrimitiveType.LineList, 0, 3);
            }
        }

        public void OnScreenResized(int width, int height)
        {
            _height = height;
            _width = width;
        }
    }
}