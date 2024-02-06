#if !FINAL

using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components.Editor;

public class AxisComponent : DrawableGameComponent, IGameComponentResizable
{
    private CasaEngineGame? _game;
    private VertexBuffer? _vertexBuffer;
    private Effect? _effect;
    private int _width;
    private int _height;

    public AxisComponent(Microsoft.Xna.Framework.Game game) : base(game)
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
    }

    public override void Draw(GameTime gameTime)
    {
        var camera = _game.GameManager.ActiveCamera;

        if (camera != null)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.SetVertexBuffer(_vertexBuffer);
            GraphicsDevice.Indices = null;

            //TODO : compute with screen height/width and aspect ratio
            var forwardFactor = (float)_width / 800f * 20f;
            var leftFactor = (float)_width / 800f * 13f;
            var upFactor = (float)_height / 480f * 6f;

            var viewMatrix = Matrix.Invert(camera.ViewMatrix);
            var position = viewMatrix.Translation + viewMatrix.Forward * forwardFactor + viewMatrix.Left * leftFactor - viewMatrix.Up * upFactor;
            var world = MatrixExtensions.Transformation(Vector3.One, Quaternion.Identity, position);
            _effect.Parameters["WorldViewProj"].SetValue(world * camera.ViewMatrix * camera.ProjectionMatrix);

            for (var i = 0; i < _effect.CurrentTechnique.Passes.Count; i++)
            {
                _effect.CurrentTechnique.Passes[i].Apply();
                GraphicsDevice.DrawPrimitives(PrimitiveType.LineList, 0, 3);
            }
        }

        base.Draw(gameTime);
    }

    public void OnScreenResized(int width, int height)
    {
        _height = height;
        _width = width;
    }
}

#endif