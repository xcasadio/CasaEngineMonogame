using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Math;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Physics.Bepu;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.TileMap;

/// <summary>
/// Regression coverage for the RPGDemo "hit breakable grass" crash: two independent cutters (e.g. the
/// player's sword and a thrown rock) overlapping the same merged grass rectangle in the same physics-event
/// batch. The first cutter's <c>OnHit</c> handler calls <c>TileCollisionManager.RemoveTile()</c>, which
/// rebuilds the tile map's collision chunk and destroys the body/manager the second cutter's already-queued
/// collision still refers to. Before the fix, that second dispatch called
/// <see cref="TileCollisionManager.GetTileData"/> on a manager whose cell had already gone back to
/// <c>EmptyTileId</c> (-1), throwing <c>KeyNotFoundException</c> out of <c>TileSetData.GetTileData</c>.
///
/// The fix spans three collaborators, each covered here:
/// - <see cref="TileCollisionManager"/> gets a lifecycle (<see cref="TileCollisionManager.IsAttached"/> /
///   <c>Detach()</c>) and never throws on a dead/empty cell.
/// - <see cref="TileMapComponent"/> detaches a tile body's manager and clears the physics world's collision
///   bookkeeping for that manager (the actual collider), not for the component, when the body is removed.
/// - <see cref="BepuPhysicsEngine"/> skips dispatching a collision that was already ended earlier in the
///   same event batch, and makes <c>ClearCollisionDataOf</c> safe against being re-entered from inside an
///   <c>OnHitEnded</c> handler it is itself calling.
/// </summary>
public class TileMapBreakableTileTests
{
    private const int GrassLayerIndex = 1;
    private const int GrassTileId = 3098;

    private sealed class CutterComponent : ICollideableComponent
    {
        public CutterComponent(Entity owner)
        {
            Owner = owner;
        }

        public Entity Owner { get; }
        public PhysicsType PhysicsType => PhysicsType.Kinetic;
        public HashSet<Collision> Collisions { get; } = new();
    }

    /// <summary>Stands in for ScriptPlayerWeapon/ScriptEnemyWeapon's HitWithGrass handler.</summary>
    private sealed class GrassBreakerProxy : GameplayProxy
    {
        public int Hits;
        public int HitEndedCount;
        public TileCollisionManager LastManager;

        public override void InitializeWithWorld(World world)
        {
        }

        public override void Update(float elapsedTime)
        {
        }

        public override void Draw()
        {
        }

        public override void OnHit(Collision collision)
        {
            Hits++;
            var manager = ResolveManager(collision);
            if (manager == null)
            {
                return;
            }

            LastManager = manager;
            var tileData = manager.GetTileData();
            if (tileData?.IsBreakable == true)
            {
                manager.RemoveTile();
            }
        }

        public override void OnHitEnded(Collision collision)
        {
            if (ResolveManager(collision) != null)
            {
                HitEndedCount++;
            }
        }

        public override void OnBeginPlay(World world)
        {
        }

        public override void OnEndPlay(World world)
        {
        }

        public override IGameplayProxy Clone() => throw new NotSupportedException();

        private TileCollisionManager ResolveManager(Collision collision)
        {
            if (collision.ColliderA.Owner == Owner)
            {
                return collision.ColliderB as TileCollisionManager;
            }

            if (collision.ColliderB.Owner == Owner)
            {
                return collision.ColliderA as TileCollisionManager;
            }

            return null;
        }
    }

    /// <summary>A proxy whose OnHitEnded re-enters the physics world's collision teardown for another
    /// component, mimicking the tilemap's chunk rebuild firing from inside a collision callback.</summary>
    private sealed class ReentrantEndProxy : GameplayProxy
    {
        public PhysicsWorld PhysicsWorld;
        public ICollideableComponent ComponentToClearOnFirstEnd;
        public int HitEndedCount;
        private bool _reentered;

        public override void InitializeWithWorld(World world)
        {
        }

        public override void Update(float elapsedTime)
        {
        }

        public override void Draw()
        {
        }

        public override void OnHit(Collision collision)
        {
        }

        public override void OnHitEnded(Collision collision)
        {
            HitEndedCount++;
            if (!_reentered && ComponentToClearOnFirstEnd != null)
            {
                _reentered = true;
                PhysicsWorld.ClearCollisionDataFrom(ComponentToClearOnFirstEnd);
            }
        }

        public override void OnBeginPlay(World world)
        {
        }

        public override void OnEndPlay(World world)
        {
        }

        public override IGameplayProxy Clone() => throw new NotSupportedException();
    }

    private sealed class CountingEndProxy : GameplayProxy
    {
        public int HitEndedCount;

        public override void InitializeWithWorld(World world)
        {
        }

        public override void Update(float elapsedTime)
        {
        }

        public override void Draw()
        {
        }

        public override void OnHit(Collision collision)
        {
        }

        public override void OnHitEnded(Collision collision)
        {
            HitEndedCount++;
        }

        public override void OnBeginPlay(World world)
        {
        }

        public override void OnEndPlay(World world)
        {
        }

        public override IGameplayProxy Clone() => throw new NotSupportedException();
    }

    private sealed class TileMapFixture
    {
        public PhysicsWorld PhysicsWorld;
        public TileMapComponent Component;
        public TileMapData TileMapData;
        public TileSetData TileSet;
        public int Width;
        public int Height;

        public TileMapLayerData GrassLayerData => TileMapData.Layers[GrassLayerIndex];

        public int RemainingGrassTiles() => GrassLayerData.tiles.Count(t => t == GrassTileId);
    }

    private static TileMapFixture BuildTileMapFixture()
    {
        var root = FindRepoRoot();
        var physicsWorld = new PhysicsWorld(useExternalViewManagement: false);
        var world = new World();
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetProperty(world, nameof(World.Game), game);
        typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, new GameComponentCollection());

        var tileMapEntity = new Entity { Name = "tileMap" };
        SetProperty(tileMapEntity, nameof(Entity.World), world);
        var component = new TileMapComponent();
        tileMapEntity.RootComponent = component;

        var tileSet = new TileSetData();
        tileSet.Load(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(root, "Projects/RPGDemo/Maps/tile1.tileset"))));
        var tileMapData = new TileMapData();
        tileMapData.Load(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(root, "Projects/RPGDemo/Maps/map_1_1.tileMap"))));
        component.TileMapData = tileMapData;
        component.TileSetData = tileSet;
        GetField<List<TileSetData>>(component, "_tileSets").Add(tileSet);
        GetField<List<Texture2D>>(component, "_tileSetTextures").Add(null);
        SetField(component, "_physicsWorldContext", physicsWorld);

        var layers = GetProperty<List<TileMapLayer>>(component, "Layers");
        int w = tileMapData.MapSize.Width, h = tileMapData.MapSize.Height;
        for (int li = 0; li < tileMapData.Layers.Count; li++)
        {
            var layerData = tileMapData.Layers[li];
            var layer = new TileMapLayer(layerData);
            for (int i = 0; i < w * h; i++)
            {
                var tile = (Tile)typeof(TileMapComponent).GetMethod("CreateRuntimeTile", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, new object[] { layerData, li, i % w, i / w })!;
                layer.Tiles.Add(tile);
                layer.CollisionObjects.Add(null);
            }

            layers.Add(layer);
            Invoke(component, "BuildChunks", layer, li);
            Invoke(component, "RebuildLayerCollisionChunks", li);
        }

        return new TileMapFixture
        {
            PhysicsWorld = physicsWorld,
            Component = component,
            TileMapData = tileMapData,
            TileSet = tileSet,
            Width = w,
            Height = h,
        };
    }

    private static (GrassBreakerProxy Proxy, PhysicsBody Body) AddCutterAtTile(TileMapFixture fixture, int tileX, int tileY, string name)
    {
        var tilePx = fixture.TileSet.TileSize.Width;
        var wm = fixture.Component.WorldMatrixWithScale;
        var pos = TileMapComponent.ComputeCollisionWorldPosition(ref wm, tileX * tilePx, tileY * tilePx, tilePx, tilePx);

        var entity = new Entity { Name = name };
        var proxy = new GrassBreakerProxy();
        SetProperty(entity, nameof(Entity.GameplayProxy), (IGameplayProxy)proxy);
        proxy.Initialize(entity);

        var component = new CutterComponent(entity);
        var matrix = Matrix.CreateTranslation(pos);
        var body = fixture.PhysicsWorld.AddGhostObject(new Box { Size = new Vector3(20f, 20f, 1f) }, Vector3.One, ref matrix, component, CollisionProfileIds.AttackVolume);
        return (proxy, body);
    }

    private static int FirstGrassTileIndex(TileMapFixture fixture) => fixture.GrassLayerData.tiles.IndexOf(GrassTileId);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    private static T GetField<T>(object target, string name)
        => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void SetField(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static T GetProperty<T>(object target, string name)
        => (T)target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void SetProperty<TTarget, TValue>(TTarget target, string name, TValue value)
        => typeof(TTarget).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);

    private static void Invoke(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);

    [Fact]
    public void TwoCuttersHittingTheSameGrassRectangle_InOneFrame_DoNotThrow()
    {
        var fixture = BuildTileMapFixture();
        var firstGrass = FirstGrassTileIndex(fixture);
        var gx = firstGrass % fixture.Width;
        var gy = firstGrass / fixture.Width;

        var sword = AddCutterAtTile(fixture, gx, gy, "sword");
        var rock = AddCutterAtTile(fixture, gx, gy, "rock");

        var exception = Record.Exception(() => fixture.PhysicsWorld.Update(1f / 60f));
        Assert.Null(exception);

        // The tile was removed even though two cutters overlapped it: the first cutter's OnHit removed it
        // and the second either saw a null tile or was skipped outright because its collision was ended
        // mid-batch by the first cutter's removal - either way, no exception and a consistent map state.
        Assert.NotEqual(GrassTileId, fixture.GrassLayerData.tiles[firstGrass]);
        Assert.True(sword.Proxy.Hits >= 1);
        Assert.True(rock.Proxy.Hits >= 1);

        // A subsequent frame must still run cleanly.
        var secondFrameException = Record.Exception(() => fixture.PhysicsWorld.Update(1f / 60f));
        Assert.Null(secondFrameException);
        fixture.Component.Update(1f / 60f);
    }

    [Fact]
    public void SingleCutterSweep_RemovesEveryGrassTile()
    {
        var fixture = BuildTileMapFixture();
        var tilePx = fixture.TileSet.TileSize.Width;

        var entity = new Entity { Name = "sweeper" };
        var proxy = new GrassBreakerProxy();
        SetProperty(entity, nameof(Entity.GameplayProxy), (IGameplayProxy)proxy);
        proxy.Initialize(entity);
        var cutterComponent = new CutterComponent(entity);

        PhysicsBody body = null;
        for (var y = 0; y < fixture.Height; y++)
        {
            for (var x = 0; x < fixture.Width; x++)
            {
                var wm = fixture.Component.WorldMatrixWithScale;
                var pos = TileMapComponent.ComputeCollisionWorldPosition(ref wm, x * tilePx, y * tilePx, tilePx, tilePx);
                var matrix = Matrix.CreateTranslation(pos);

                if (body == null)
                {
                    body = fixture.PhysicsWorld.AddGhostObject(new Box { Size = new Vector3(20f, 20f, 1f) }, Vector3.One, ref matrix, cutterComponent, CollisionProfileIds.AttackVolume);
                }
                else
                {
                    body.WorldTransform = matrix;
                    body.RefreshAabb();
                }

                var exception = Record.Exception(() => fixture.PhysicsWorld.Update(1f / 60f));
                Assert.Null(exception);
                fixture.Component.Update(1f / 60f);
            }
        }

        Assert.Equal(0, fixture.RemainingGrassTiles());
    }

    [Fact]
    public void DetachedManager_AnswersSafely()
    {
        var fixture = BuildTileMapFixture();
        var firstGrass = FirstGrassTileIndex(fixture);
        var gx = firstGrass % fixture.Width;
        var gy = firstGrass / fixture.Width;

        var cutter = AddCutterAtTile(fixture, gx, gy, "sword");
        fixture.PhysicsWorld.Update(1f / 60f);

        var manager = cutter.Proxy.LastManager;
        Assert.NotNull(manager);
        Assert.False(manager.IsAttached);

        Assert.Null(manager.GetTileData());

        var tileCountBefore = fixture.RemainingGrassTiles();
        manager.RemoveTile();
        Assert.Equal(tileCountBefore, fixture.RemainingGrassTiles());
    }

    [Fact]
    public void ChunkRebuild_EndsTheCollisionsOfItsOldManagers()
    {
        var fixture = BuildTileMapFixture();
        var firstGrass = FirstGrassTileIndex(fixture);
        var gx = firstGrass % fixture.Width;
        var gy = firstGrass / fixture.Width;

        var entity = new Entity { Name = "sword" };
        var countingProxy = new CountingEndProxy();
        SetProperty(entity, nameof(Entity.GameplayProxy), (IGameplayProxy)countingProxy);
        countingProxy.Initialize(entity);
        var cutterComponent = new CutterComponent(entity);

        var tilePx = fixture.TileSet.TileSize.Width;
        var wm = fixture.Component.WorldMatrixWithScale;
        var pos = TileMapComponent.ComputeCollisionWorldPosition(ref wm, gx * tilePx, gy * tilePx, tilePx, tilePx);
        var matrix = Matrix.CreateTranslation(pos);
        var body = fixture.PhysicsWorld.AddGhostObject(new Box { Size = new Vector3(20f, 20f, 1f) }, Vector3.One, ref matrix, cutterComponent, CollisionProfileIds.AttackVolume);

        fixture.PhysicsWorld.Update(1f / 60f);
        Assert.Single(cutterComponent.Collisions);
        var collision = cutterComponent.Collisions.Single();
        var manager = (collision.ColliderA as TileCollisionManager) ?? (collision.ColliderB as TileCollisionManager);
        Assert.NotNull(manager);

        manager.RemoveTile();

        // The rebuild detaches and clears the old manager's collision synchronously, before the next Update.
        Assert.Empty(cutterComponent.Collisions);
        Assert.Equal(1, countingProxy.HitEndedCount);

        fixture.PhysicsWorld.Update(1f / 60f);
        Assert.Equal(1, countingProxy.HitEndedCount);
    }

    [Fact]
    public void ClearCollisionData_IsReentrant()
    {
        var physicsWorld = new PhysicsWorld(useExternalViewManagement: true);

        var xEntity = new Entity { Name = "x" };
        var xProxy = new ReentrantEndProxy { PhysicsWorld = physicsWorld };
        SetProperty(xEntity, nameof(Entity.GameplayProxy), (IGameplayProxy)xProxy);
        xProxy.Initialize(xEntity);
        var xComponent = new CutterComponent(xEntity);
        var xMatrix = Matrix.Identity;
        var xBody = physicsWorld.AddGhostObject(new Box { Size = new Vector3(100f, 100f, 1f) }, Vector3.One, ref xMatrix, xComponent, CollisionProfileIds.AttackVolume);

        // M1 and M2 both sit inside X's (large) box but far enough apart from each other that they do not
        // also overlap one another - the test isolates the X-M1/X-M2 pair from any M1-M2 collision.
        var m1Entity = new Entity { Name = "m1" };
        var m1Proxy = new CountingEndProxy();
        SetProperty(m1Entity, nameof(Entity.GameplayProxy), (IGameplayProxy)m1Proxy);
        m1Proxy.Initialize(m1Entity);
        var m1Component = new CutterComponent(m1Entity);
        var m1Matrix = Matrix.CreateTranslation(40f, 0f, 0f);
        physicsWorld.AddGhostObject(new Box { Size = new Vector3(10f, 10f, 1f) }, Vector3.One, ref m1Matrix, m1Component, CollisionProfileIds.AttackVolume);

        var m2Entity = new Entity { Name = "m2" };
        var m2Proxy = new CountingEndProxy();
        SetProperty(m2Entity, nameof(Entity.GameplayProxy), (IGameplayProxy)m2Proxy);
        m2Proxy.Initialize(m2Entity);
        var m2Component = new CutterComponent(m2Entity);
        var m2Matrix = Matrix.CreateTranslation(-40f, 0f, 0f);
        physicsWorld.AddGhostObject(new Box { Size = new Vector3(10f, 10f, 1f) }, Vector3.One, ref m2Matrix, m2Component, CollisionProfileIds.AttackVolume);

        var yEntity = new Entity { Name = "y" };
        var yProxy = new CountingEndProxy();
        SetProperty(yEntity, nameof(Entity.GameplayProxy), (IGameplayProxy)yProxy);
        yProxy.Initialize(yEntity);
        var yComponent = new CutterComponent(yEntity);
        var yMatrix = Matrix.CreateTranslation(1000f, 0f, 0f);
        physicsWorld.AddGhostObject(new Box { Size = new Vector3(50f, 50f, 1f) }, Vector3.One, ref yMatrix, yComponent, CollisionProfileIds.AttackVolume);

        var zEntity = new Entity { Name = "z" };
        var zProxy = new CountingEndProxy();
        SetProperty(zEntity, nameof(Entity.GameplayProxy), (IGameplayProxy)zProxy);
        zProxy.Initialize(zEntity);
        var zComponent = new CutterComponent(zEntity);
        var zMatrix = Matrix.CreateTranslation(1001f, 0f, 0f);
        physicsWorld.AddGhostObject(new Box { Size = new Vector3(50f, 50f, 1f) }, Vector3.One, ref zMatrix, zComponent, CollisionProfileIds.AttackVolume);

        // X overlaps both M1 and M2; Y overlaps Z, far away and unrelated. Establish all four collisions.
        physicsWorld.Update(1f / 60f);
        Assert.Equal(2, xComponent.Collisions.Count);
        Assert.Single(yComponent.Collisions);
        Assert.Single(zComponent.Collisions);

        // When the engine ends X's collisions, X's own OnHitEnded (fired for the first of the two) reaches
        // back into the physics world to clear Y's collision - re-entering ClearCollisionDataOf while it is
        // still iterating X's own pending-removal list.
        xProxy.ComponentToClearOnFirstEnd = yComponent;
        var exception = Record.Exception(() => physicsWorld.ClearCollisionDataFrom(xComponent));
        Assert.Null(exception);

        // Neither X's collisions nor the reentrantly-cleared Y/Z collision were missed.
        Assert.Empty(xComponent.Collisions);
        Assert.Empty(m1Component.Collisions);
        Assert.Empty(m2Component.Collisions);
        Assert.Empty(yComponent.Collisions);
        Assert.Empty(zComponent.Collisions);
        Assert.Equal(1, m1Proxy.HitEndedCount);
        Assert.Equal(1, m2Proxy.HitEndedCount);
        Assert.Equal(1, yProxy.HitEndedCount);
        Assert.Equal(1, zProxy.HitEndedCount);

        // A normal Update afterwards must not resurrect or double-fire anything for the cleared pairs.
        var afterException = Record.Exception(() => physicsWorld.Update(1f / 60f));
        Assert.Null(afterException);
        Assert.Equal(1, m1Proxy.HitEndedCount);
        Assert.Equal(1, m2Proxy.HitEndedCount);
        Assert.Equal(1, yProxy.HitEndedCount);
        Assert.Equal(1, zProxy.HitEndedCount);
    }
}
