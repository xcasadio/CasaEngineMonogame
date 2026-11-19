using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Video;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Video;

/// <summary>
/// A world over a game built without its constructor, with a real <see cref="AssetContentManager"/> that serves one
/// video container, and a real <see cref="SpriteRendererComponent"/> whose queue the tests inspect. The manager has a
/// graphics device only when one is given.
/// </summary>
internal sealed class WorldVideoHarness
{
    public WorldVideoHarness(WorldVideoTestFiles.Loader loader, GraphicsDevice device = null)
    {
        Game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(Game, new GameComponentCollection());

        var gameManager = (GameManager)RuntimeHelpers.GetUninitializedObject(typeof(GameManager));
        typeof(GameManager).GetField("<ViewManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(gameManager, new CasaEngine.Framework.Rendering.ViewManager());
        typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(Game, gameManager);
        Game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;

        Manager = new AssetContentManager();
        if (device != null)
        {
            Manager.Initialize(device);
        }

        if (loader != null)
        {
            Manager.RegisterAssetLoader(typeof(MdecVideoAsset), loader);
        }

        typeof(CasaEngineGame).GetField("<AssetContentManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(Game, Manager);

        Renderer = new SpriteRendererComponent(Game);
        if (device != null)
        {
            // The renderer reads the scissor rectangle of the device through its game, as it does in a running game.
            var service = (IGraphicsDeviceService)GpuDeviceHost.Instance.Game.Services.GetService(typeof(IGraphicsDeviceService));
            typeof(Game)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(IGraphicsDeviceService))
                .SetValue(Game, service);
        }

        World = new World { Name = "WorldVideoHarness" };
        typeof(World).GetProperty(nameof(World.Game), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(World, Game);
    }

    public CasaEngineGame Game { get; }

    public AssetContentManager Manager { get; }

    public SpriteRendererComponent Renderer { get; }

    public World World { get; }

    /// <summary>The queue of the renderer: its entries are private structs, read by reflection.</summary>
    public System.Collections.IList Queue
    {
        get
        {
            var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
            return (System.Collections.IList)field!.GetValue(Renderer)!;
        }
    }

    /// <summary>
    /// Makes the renderer able to flush on the real device of <paramref name="game"/>, as its LoadContent does for the
    /// game it is created with: the graphics device service, the sprite effect, the vertex and the index buffers.
    /// </summary>
    public void PrepareRendering(Game game)
    {
        var service = (IGraphicsDeviceService)game.Services.GetService(typeof(IGraphicsDeviceService));
        var device = service.GraphicsDevice;
        var type = typeof(SpriteRendererComponent);
        type.GetField("_effect", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(Renderer, game.Content.Load<Effect>(@"Shaders\SpriteBatch"));
        type.GetField("_vertexBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(Renderer, new VertexBuffer(device, typeof(VertexPositionTexture), 4 * 16, BufferUsage.None));
        var indexBuffer = new IndexBuffer(device, typeof(short), 6, BufferUsage.None);
        indexBuffer.SetData(new short[] { 0, 1, 2, 0, 2, 3 });
        type.GetField("_indexBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Renderer, indexBuffer);
    }

    /// <summary>Initializes the entity in the world, as the world does when it adds an entity.</summary>
    public void AddToWorld(Entity entity)
    {
        entity.Initialize();
        entity.InitializeWithWorld(World);
    }

    public static object Field(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field!.GetValue(instance)!;
    }
}

/// <summary>Like <see cref="TheoryAttribute"/>, but skips when no real graphics device can be created in this process.</summary>
public sealed class GpuTheoryAttribute : TheoryAttribute
{
    public GpuTheoryAttribute()
    {
        if (!CasaEngine.Tests.UI.Backend.GpuDeviceHost.Instance.IsAvailable)
        {
            Skip = CasaEngine.Tests.UI.Backend.GpuDeviceHost.Instance.UnavailableReason;
        }
    }
}
