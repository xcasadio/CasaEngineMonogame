#if !FINAL

using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components.DebugTools
{
    public class DebugAxisComponent : DrawableGameComponent, IGameComponentResizable
    {
        private CasaEngineGame? _game;
        private VertexBuffer? _vertexBuffer;
        private Effect? _effect;
        private int _width;
        private int _height;

        public DebugAxisComponent(Microsoft.Xna.Framework.Game game) : base(game)
        {
            _game = game as CasaEngineGame;
            game.Components.Add(this);
            UpdateOrder = (int)ComponentUpdateOrder.Manipulator;
            DrawOrder = (int)ComponentDrawOrder.Manipulator;
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            //var font = Game.Content.Load<SpriteFont>("GizmoFont");
            _effect = Game.Content.Load<Effect>("Shaders\\axisComponent");

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

        public override void Draw(GameTime gameTime)
        {
            // Visible = false — this override is never called from DrawWithEditor.
            // Drawing is done by DrawForView() called from OverlayViewPipeline.
            base.Draw(gameTime);
        }

        /// <summary>
        /// Draws the axis orientation indicator for a specific view.
        /// Uses <paramref name="frame"/>.ViewportRect for the pixel dimensions so the
        /// indicator is always scaled correctly regardless of the viewport size.
        /// Called by <see cref="OverlayViewPipeline"/> with the view's render target active.
        /// </summary>
        public void DrawForView(GraphicsDevice gd, in RenderFrame frame)
        {
            int width  = frame.ViewportRect.Width;
            int height = frame.ViewportRect.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState   = RasterizerState.CullNone;
            gd.BlendState        = BlendState.Opaque;
            gd.SetVertexBuffer(_vertexBuffer);
            gd.Indices = null;

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
            _effect.Parameters["WorldViewProj"].SetValue(world * frame.View * frame.Projection);

            for (var i = 0; i < _effect.CurrentTechnique.Passes.Count; i++)
            {
                _effect.CurrentTechnique.Passes[i].Apply();
                gd.DrawPrimitives(PrimitiveType.LineList, 0, 3);
            }
        }

        public void OnScreenResized(int width, int height)
        {
            _height = height;
            _width = width;
        }
    }
}

namespace CasaEngine.Framework.Game.Components.Editor
{
    [Obsolete("Use CasaEngine.Framework.Game.Components.DebugTools.DebugAxisComponent instead.")]
    public sealed class AxisComponent : DebugTools.DebugAxisComponent
    {
        public AxisComponent(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
        }
    }
}

#endif