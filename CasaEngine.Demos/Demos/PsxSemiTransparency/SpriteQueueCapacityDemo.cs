using System;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos.PsxSemiTransparency;

/// <summary>
/// E19.g G2a (ADR-0051): the sprite queue is not capped at 10 000 entries. A component queues 12 001 sprites of one texel
/// each in increasing sort-key order (a background first, then 11 999 filler texels in the top left corner, then one green
/// texel at the witness pixel, last in the sort order), so the witness sprite sits past the old 10 000 entry limit of the
/// vertex staging array and of the vertex buffer. The scene reads its own back-buffer in process
/// (<see cref="BackBufferProbe"/>) and expects the green texel at the witness pixel, to within one level per channel:
/// run it with <c>CASAENGINE_START_DEMO="Sprite queue capacity"</c> from the <c>CasaEngine.Demos</c> folder.
/// </summary>
public class SpriteQueueCapacityDemo : Demo
{
    private static readonly Color Background = new(100, 150, 200, 255);
    private static readonly Color Witness = new(0, 255, 0, 255);

    private const int FillerCount = 11999;
    private const int FillerColumns = 100;
    private static readonly Vector3 WitnessCenter = new(702f, 298f, 0f);

    private readonly BackBufferProbe _probe = new("Sprite queue capacity");
    private Texture2D? _texture;

    public override string Title => "Sprite queue capacity";

    public override string Description =>
        "Queues 12 001 one-texel sprites in one frame: the last one in the sort order, a green texel, is past the old limit of " +
        "10 000 entries and must still reach the screen. The scene checks its own back-buffer.";

    public override void Initialize(CasaEngineGame game)
    {
        // Sheet of three texels: filler (white), witness (green), background.
        _texture = new Texture2D(game.GraphicsDevice, 3, 1, false, SurfaceFormat.Color);
        _texture.SetData(new[] { Color.White, Witness, Background });

        var entity = new Entity { Name = "SpriteQueueStress" };
        entity.RootComponent = new SpriteQueueStressComponent(_texture);
        game.GameManager.CurrentWorld.AddEntity(entity);

        _probe.Add("witness texel (the 12 001st queued sprite)", WitnessCenter, Witness);
        _probe.Add("background beside the witness", new Vector3(WitnessCenter.X + 40f, WitnessCenter.Y, 0f), Background);
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity();
        var camera = new Camera2dComponent();
        camera.Target = new Vector3(game.Window.ClientBounds.Size.X / 2f, game.Window.ClientBounds.Size.Y / 2f, 0.0f);
        entity.AddComponent(camera);
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);

        return camera;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        // The camera is already framed on the window centre by CreateCamera.
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        _probe.OnPostDraw(game);
    }

    public override void Clean()
    {
        _texture?.Dispose();
        _texture = null;
    }

    /// <summary>Queues the stress sprites each frame, straight on the sprite renderer.</summary>
    private sealed class SpriteQueueStressComponent : SceneComponent
    {
        private readonly Texture2D _texture;
        private SpriteRendererComponent? _renderer;

        public SpriteQueueStressComponent(Texture2D texture)
        {
            _texture = texture;
        }

        private SpriteQueueStressComponent(SpriteQueueStressComponent other) : base(other)
        {
            _texture = other._texture;
        }

        public override SpriteQueueStressComponent Clone() => new(this);

        public override void InitializeWithWorld(World world)
        {
            base.InitializeWithWorld(world);
            _renderer = world.Game.GetGameComponent<SpriteRendererComponent>();
        }

        public override void Draw(float elapsedTime)
        {
            if (_renderer == null)
            {
                return;
            }

            var scissor = _renderer.GraphicsDevice.ScissorRectangle;

            // Sort key 0: the background, behind every texel (the keys of the others start at 1).
            var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, 0, 0, 0, 0, 0);
            _renderer.DrawSprite(_texture, new Rectangle(2, 0, 1, 1), Point.Zero, new Vector2(-1500f, 2500f), 0f,
                new Vector2(4000f, 4000f), Color.White, 0f, in key, SpriteEffects.None, scissor);

            for (var index = 1; index <= FillerCount; index++)
            {
                key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, index, 0, 0, 0, 0);
                var column = (index - 1) % FillerColumns;
                var row = (index - 1) / FillerColumns;
                _renderer.DrawSprite(_texture, new Rectangle(0, 0, 1, 1), Point.Zero,
                    new Vector2(4f + column * 2f, 700f - row * 2f), 0f, Vector2.One, Color.White, 0f, in key,
                    SpriteEffects.None, scissor);
            }

            // The 12 001st entry: the last in the sort order, four texels wide at the witness pixel.
            key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, FillerCount + 1, 0, 0, 0, 0);
            _renderer.DrawSprite(_texture, new Rectangle(1, 0, 1, 1), Point.Zero,
                new Vector2(WitnessCenter.X - 2f, WitnessCenter.Y + 2f), 0f, new Vector2(4f, 4f), Color.White, 0f, in key,
                SpriteEffects.None, scissor);
        }
    }
}
