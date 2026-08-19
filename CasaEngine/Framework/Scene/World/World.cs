using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.Scripting.Coroutines;
using CasaEngine.Framework.Scene.CharacterMotion;
using CasaEngine.Framework.Scene.Transform;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.World;

public sealed class World : ObjectBase
{
    private readonly List<EntityReference> _entityReferences = [];
    private readonly List<Entity> _entities = [];
    private readonly List<Entity> _baseObjectsToAdd = [];
    private readonly List<WorldUIComponent> _worldUiComponents = [];
    private readonly List<TileMapSurfaceComponent> _tileMapSurfaces = [];
    private readonly HashSet<Entity> _observedEntities = [];
    private readonly HashSet<Guid> _warnedPolicyEntities = [];

    private readonly List<Entity> _entitiesVisible = new(1000);

    private readonly List<PlayerController> _playerControllers = [];

    public CasaEngineGame Game { get; private set; }
    public IPhysicsWorld PhysicsWorld { get; private set; } = null!;
    public IList<Entity> Entities => _entities;
    public string GameplayProxyClassName { get; set; }

    /// <summary>
    /// Simulation space policy this world declares, by name. Empty means the project default.
    /// It is resolved when the physics context of the world is created, so it must be set before
    /// <see cref="LoadContent(CasaEngineGame)"/> runs.
    /// </summary>
    public string SpacePolicyName { get; set; } = string.Empty;
    /// <summary>
    /// Collision field of this world, or null when it has none. A field carries dense environment
    /// data (ground height, walkability, surface tag) sampled analytically; it is never expressed as
    /// collision geometry and never inserted into <see cref="PhysicsWorld"/>.
    /// It is NOT serialized: a field comes from code or from converted data, never from the world file.
    /// Ownership of the reset follows the two clearing paths: <see cref="Clear"/> drops it as part of
    /// the full teardown, while <see cref="ClearEntities"/> leaves it untouched, so loading a world
    /// does not silently discard a field assigned from code.
    /// </summary>
    public ICollisionField CollisionField { get; set; }

    public GameplayProxy GameplayProxy { get; private set; }

    //state loaded for the proxy, applied when the proxy is created; kept afterwards so
    //a world saved before its content is loaded does not lose it
    internal JObject GameplayProxyState => _gameplayProxyState;
    private JObject _gameplayProxyState;
    public Guid PlayerStartupSettingsAssetId { get; set; } = Guid.Empty;
    public PlayerStartupSettings PlayerStartupSettings { get; private set; } = new();
    public Guid GameplayModeAssetId { get; set; } = Guid.Empty;
    public GameplayModeRunner GameplayModeRunner { get; } = new();
    public GameplayEventBus GameplayEvents => GameplayModeRunner.Events;
    public int UpdateSequence { get; private set; }
    public WorldSpatialServices SpatialServices { get; }
    public WorldEnvironmentSettings EnvironmentSettings { get; } = new();
    public ReflectionProbeCollection ReflectionProbes { get; } = new();
    public IReadOnlyList<PlayerController> PlayerControllers => _playerControllers;
    public IWorldMessageBus MessageBus { get; }
    public WorldRuntimeSystems RuntimeSystems { get; }
    public CharacterMotionSystem CharacterMotion => RuntimeSystems.CharacterMotion;
    public CoroutineManager CoroutineManager => RuntimeSystems.CoroutineManager;
    public CutsceneDirector CutsceneDirector => RuntimeSystems.CutsceneDirector;
    public EntityPolicyDiagnosticsSnapshot PolicyDiagnostics { get; private set; } = EntityPolicyDiagnosticsSnapshot.Empty;
    public RenderFrame? CurrentRenderFrame { get; private set; }

    public bool DisplaySpacePartitioning { get; set; }


    public event EventHandler EntitiesClear;
    public event EventHandler EntitiesCleared;
    public event EventHandler<Entity> EntityAdded;
    public event EventHandler<Entity> EntityRemoved;


    public World()
        : this(null)
    {
    }

    public World(Func<World, WorldSpatialServices> spatialServicesFactory)
    {
        MessageBus = new WorldMessageBus();
        Func<World, WorldSpatialServices> resolvedSpatialServicesFactory = spatialServicesFactory ?? (world => WorldSpatialServices.CreateDefault(world));
        SpatialServices = resolvedSpatialServicesFactory(this);
        RuntimeSystems = new WorldRuntimeSystems(this);
    }

    public void Clear()
    {
        // Mirror BeginPlay: notify scripts before destroying anything.
        GameplayProxy?.OnEndPlay(this);

        foreach (var entity in _entities)
            entity.GameplayProxy?.OnEndPlay(this);

        GameplayModeRunner.Stop();
        RuntimeSystems.Clear();

        foreach (var worldUiComponent in _worldUiComponents)
        {
            worldUiComponent.Dispose();
        }

        _worldUiComponents.Clear();

        foreach (var tileMapSurface in _tileMapSurfaces)
        {
            tileMapSurface.Dispose();
        }

        _tileMapSurfaces.Clear();

        ClearEntities(true);
        CollisionField = null;
        DisposePhysicsWorldContext();
    }

    public Entity SpawnEntity<T>(string assetName) where T : Entity
    {
        var assetInfo = AssetCatalog.Get(assetName);
        var entity = Game.AssetContentManager.Load<Entity>(assetInfo.Id).Clone();
        AddEntity(entity);
        return entity;
    }

    public T SpawnEntity<T>(Guid id) where T : Entity
    {
        var entity = Game.AssetContentManager.Load<T>(id, cache: false);
        AddEntity(entity);
        return (T)entity;
    }

    public void AddEntity(Entity entity)
    {
        System.Diagnostics.Debug.Assert(entity != null, "AddEntity() : Actor can't be null");
        Logs.WriteTrace($"Entity waiting to be added : {entity.Name} {entity.Id}");
        _baseObjectsToAdd.Add(entity);
    }

    public void RemoveEntity(Entity entity)
    {
        entity.Destroy();
    }

    public void SetGameplayMode(GameplayMode mode)
    {
        ArgumentNullException.ThrowIfNull(mode);

        GameplayModeRunner.Start(mode, new GameplayContext(this, GameplayModeRunner.Events));
    }

    public void ClearEntities(bool clearReferences = false)
    {
        foreach (var entity in _entities)
        {
            MessageBus.UnregisterEntity(entity);
            UnsubscribeEntityTree(entity);
            entity.Destroy();
        }

        _entities.Clear();
        _baseObjectsToAdd.Clear();
        SpatialServices.WorldIndex.Clear();
        _observedEntities.Clear();
        _warnedPolicyEntities.Clear();
        MessageBus.Reset();
        PolicyDiagnostics = EntityPolicyDiagnosticsSnapshot.Empty;

        if (clearReferences)
        {
            _entityReferences.Clear();
        }

        EntitiesClear?.Invoke(this, EventArgs.Empty);
        EntitiesCleared?.Invoke(this, EventArgs.Empty);
    }

    public void LoadContent(CasaEngineGame game)
    {
        Game = game;
        InitializePhysicsWorldContext();
        LoadContent(AssetCatalog.IsLoaded);
    }

    private void InitializePhysicsWorldContext()
    {
        DisposePhysicsWorldContext();
        PhysicsWorld = Game.PhysicsSystemComponent.GetOrCreateContext(this);
    }

    private void DisposePhysicsWorldContext()
    {
        if (Game == null)
        {
            return;
        }

        Game.PhysicsSystemComponent.ReleaseContext(this);
        PhysicsWorld = null!;
    }

    private void LoadContent(bool withReference)
    {
        LoadPlayerStartupSettings();

        if (withReference)
        {
            foreach (var entityReference in _entityReferences)
            {
                LoadFromEntityReference(entityReference);
            }
        }

        //after everything initialized entities are added
        InternalAddEntities();

        if (Game.ExecutionPolicy.InitializePlayerControllers)
        {
            InitializePlayerControllers();
        }

        CreateGameplayProxy();

        if (Game.ExecutionPolicy.InitializeGameplayOnLoad)
        {
            GameplayProxy?.Initialize(null);
            GameplayProxy?.InitializeWithWorld(this);
        }

        //Maybe GameplayProxy.InitializeWithWorld() will add some entities
        //TODO : check if we have to do it only once just here
        InternalAddEntities();
    }

    //internal so tests can drive proxy creation without a full game behind the world
    internal void CreateGameplayProxy()
    {
        if (string.IsNullOrWhiteSpace(GameplayProxyClassName))
        {
            return;
        }

        GameplayProxy = ElementFactory.Create<GameplayProxy>(GameplayProxyClassName);

        if (GameplayProxy != null && _gameplayProxyState != null)
        {
            GameplayProxy.Load(_gameplayProxyState);
        }
    }

    private void LoadPlayerStartupSettings()
    {
        if (PlayerStartupSettingsAssetId != Guid.Empty)
        {
            PlayerStartupSettings = Game.AssetContentManager.Load<PlayerStartupSettings>(PlayerStartupSettingsAssetId);
        }
        else
        {
            PlayerStartupSettings = new PlayerStartupSettings();
        }
    }

    private void InitializePlayerControllers()
    {
        if (PlayerStartupSettings.DefaultPawnAssetId == Guid.Empty)
        {
            return;
        }

        var pawn = SpawnEntity<Pawn>(PlayerStartupSettings.DefaultPawnAssetId);
        var playerController = ElementFactory.Create<PlayerController>(PlayerStartupSettings.PlayerControllerClass);
        playerController.Pawn = pawn;
        playerController.Player = new LocalPlayer(); // TODO
        if (Game?.InputComponent != null)
        {
            playerController.Input = new PlayerInput(playerController, Game.InputComponent);
        }
        _playerControllers.Add(playerController);

        var playerStartComponent = GetPlayerStart((int)PlayerIndex.One);
        if (playerStartComponent != null)
        {
            pawn.RootComponent?.LocalTransform.CopyFrom(playerStartComponent.LocalTransform);
        }

        InternalAddEntities();
    }

    public CameraComponent CreateDefaultCamera()
    {
        var entityCamera = new Entity();
        var camera = new CameraLookAtComponent();
        entityCamera.RootComponent = camera;
        camera.SetPositionAndTarget(Vector3.Forward * -10, Vector3.Zero);

        entityCamera.Initialize();
        entityCamera.InitializeWithWorld(this);
        Logs.WriteWarning($"No camera found in the world {Name}. Create default one");

        return camera;
    }

    private void LoadFromEntityReference(EntityReference entityReference)
    {
        entityReference.Load(Game.AssetContentManager);
        AddEntity(entityReference.Entity);
    }

    public void BeginPlay()
    {
        if (!Game.ExecutionPolicy.RunBeginPlay)
        {
            return;
        }

        StartGameplayModeAsset();

        GameplayProxy?.OnBeginPlay(this);

        foreach (var entity in _entities)
        {
            entity.GameplayProxy?.OnBeginPlay(this);
        }
    }

    private void StartGameplayModeAsset()
    {
        if (GameplayModeAssetId == Guid.Empty)
        {
            return;
        }

        var gameplayModeAsset = Game.AssetContentManager.Load<GameplayModeAsset>(GameplayModeAssetId);
        GameplayMode mode = gameplayModeAsset.CreateMode();

        if (mode != null)
        {
            SetGameplayMode(mode);
        }
    }

    private PlayerStartComponent GetPlayerStart(int playerIndex)
    {
        PlayerStartComponent playerStartComponent = null;

        foreach (var entity in _entities)
        {
            playerStartComponent = entity.GetComponent<PlayerStartComponent>();

            if (playerStartComponent != null)
            {
                //TODO : check player index
                return playerStartComponent;
            }
        }

        return playerStartComponent;
    }

    public void Update(float elapsedTime)
    {
        Update(FrameTime.FromElapsedTime(elapsedTime, UpdateSequence + 1L));
    }

    public void Update(FrameTime frameTime)
    {
        UpdateSequence++;

        float elapsedTime = frameTime.DeltaTime;
        GameplayModeRunner.Update(frameTime);

        var toRemove = new List<Entity>();
        var diagnosticsBuilder = new EntityPolicyDiagnosticsBuilder(this);
        bool invalidateOnDemandViews = false;

        InternalAddEntities();
        SpatialServices.SteeringIndex.PrepareForWorldUpdate();
        SpatialServices.NeighborhoodService.PrepareForWorldUpdate();
        RuntimeSystems.Update(frameTime);

        foreach (var entity in _entities)
        {
            if (entity.ToBeRemoved)
            {
                toRemove.Add(entity);
                SpatialServices.WorldIndex.Remove(entity);
            }
            else
            {
                ResolvedEntityPolicies runtimePolicies = EntityPolicyResolver.ResolveRuntimePolicies(entity);
                diagnosticsBuilder.Record(entity, runtimePolicies);
                invalidateOnDemandViews |= runtimePolicies.RequiresOnDemandViewInvalidation;

                if (runtimePolicies.ShouldUpdateThisFrame)
                {
                    entity.Update(elapsedTime);
                    entity.Policies.ClearConditionalUpdateRequest();
                    diagnosticsBuilder.MarkUpdated();
                }

                if (runtimePolicies.UsesDynamicSpatialMaintenance && IsBoundingBoxDirty(entity))
                {
                    SpatialServices.WorldIndex.Move(entity, entity.GetBoundingBox());
                    entity.ClearBoundingBoxDirty();
                }
            }
        }

        foreach (var entity in toRemove)
        {
            entity.StopAllOwnedCoroutines();
            MessageBus.UnregisterEntity(entity);
            UnsubscribeEntityTree(entity);
            _entities.Remove(entity);
            NotifyEntityRemovedRecursive(entity);
        }

        SpatialServices.WorldIndex.ApplyPendingMoves();
        PolicyDiagnostics = diagnosticsBuilder.Build();

        if (invalidateOnDemandViews)
        {
            InvalidateOnDemandViews();
        }

        if (Game?.ExecutionPolicy.UpdateGameplayScripts ?? false)
        {
            GameplayProxy?.Update(elapsedTime);
        }
    }

    private bool IsBoundingBoxDirty(Entity actor)
    {
        if (actor.RootComponent?.IsBoundingBoxDirty ?? false)
        {
            return true;
        }

        foreach (var component in actor.Components)
        {
            if (component is SceneComponent { IsBoundingBoxDirty: true })
            {
                return true;
            }
        }

        return false;
    }

    private void InvalidateOnDemandViews()
    {
        var views = Game?.GameManager.ViewManager.Views;
        if (views == null)
        {
            return;
        }

        for (int index = 0; index < views.Count; index++)
        {
            RenderView view = views[index];
            if (!ReferenceEquals(view.World, this) || view.UpdateMode != ViewUpdateMode.OnDemand)
            {
                continue;
            }

            view.Invalidate();
        }
    }

    private void LogPolicyWarning(Entity entity, string reason, EntityPolicySet policySet)
    {
        if (!_warnedPolicyEntities.Add(entity.Id))
        {
            return;
        }

        Logs.WriteWarning(
            $"Entity '{entity.Name}' uses a suspect policy combination: {reason} "
            + $"(source={entity.Policies.PolicySourceMode}, mobility={policySet.Mobility}, tick={policySet.TickPolicy}, spatial={policySet.SpatialPolicy}, render={policySet.RenderDynamicPolicy}).");
    }

    private struct EntityPolicyDiagnosticsBuilder
    {
        private readonly World _world;
        private int _totalEntities;
        private int _explicitPolicyEntities;
        private int _defaultPolicyEntities;
        private int _updatedEntities;
        private int _mobilityStaticEntities;
        private int _mobilityMovableEntities;
        private int _tickNeverEntities;
        private int _tickConditionalEntities;
        private int _tickEveryFrameEntities;
        private int _spatialStaticEntities;
        private int _spatialDynamicEntities;
        private int _renderStaticEntities;
        private int _renderMaterialAnimatedEntities;
        private int _renderGeometryAnimatedEntities;
        private int _dynamicRenderEntities;
        private int _suspectCombinationCount;

        public EntityPolicyDiagnosticsBuilder(World world)
        {
            _world = world;
            _totalEntities = 0;
            _explicitPolicyEntities = 0;
            _defaultPolicyEntities = 0;
            _updatedEntities = 0;
            _mobilityStaticEntities = 0;
            _mobilityMovableEntities = 0;
            _tickNeverEntities = 0;
            _tickConditionalEntities = 0;
            _tickEveryFrameEntities = 0;
            _spatialStaticEntities = 0;
            _spatialDynamicEntities = 0;
            _renderStaticEntities = 0;
            _renderMaterialAnimatedEntities = 0;
            _renderGeometryAnimatedEntities = 0;
            _dynamicRenderEntities = 0;
            _suspectCombinationCount = 0;
        }

        public void Record(Entity entity, ResolvedEntityPolicies runtimePolicies)
        {
            _totalEntities++;

            if (runtimePolicies.SourceMode == EntityPolicySourceMode.Explicit)
            {
                _explicitPolicyEntities++;
            }
            else
            {
                _defaultPolicyEntities++;
            }

            switch (runtimePolicies.PolicySet.Mobility)
            {
                case Mobility.Static:
                    _mobilityStaticEntities++;
                    break;
                case Mobility.Movable:
                    _mobilityMovableEntities++;
                    break;
            }

            switch (runtimePolicies.PolicySet.TickPolicy)
            {
                case TickPolicy.Never:
                    _tickNeverEntities++;
                    break;
                case TickPolicy.Conditional:
                    _tickConditionalEntities++;
                    break;
                case TickPolicy.EveryFrame:
                    _tickEveryFrameEntities++;
                    break;
            }

            switch (runtimePolicies.PolicySet.SpatialPolicy)
            {
                case SpatialPolicy.StaticIndex:
                    _spatialStaticEntities++;
                    break;
                case SpatialPolicy.DynamicIndex:
                    _spatialDynamicEntities++;
                    break;
            }

            switch (runtimePolicies.PolicySet.RenderDynamicPolicy)
            {
                case RenderDynamicPolicy.Static:
                    _renderStaticEntities++;
                    break;
                case RenderDynamicPolicy.MaterialAnimated:
                    _renderMaterialAnimatedEntities++;
                    _dynamicRenderEntities++;
                    break;
                case RenderDynamicPolicy.GeometryAnimated:
                    _renderGeometryAnimatedEntities++;
                    _dynamicRenderEntities++;
                    break;
            }

            string suspectReason = EntityPolicyResolver.GetSuspectCombinationReason(runtimePolicies.PolicySet);
            if (suspectReason != null)
            {
                _suspectCombinationCount++;
                _world.LogPolicyWarning(entity, suspectReason, runtimePolicies.PolicySet);
            }
        }

        public void MarkUpdated()
        {
            _updatedEntities++;
        }

        public EntityPolicyDiagnosticsSnapshot Build()
        {
            return new EntityPolicyDiagnosticsSnapshot(
                _totalEntities,
                _explicitPolicyEntities,
                _defaultPolicyEntities,
                _updatedEntities,
                _mobilityStaticEntities,
                _mobilityMovableEntities,
                _tickNeverEntities,
                _tickConditionalEntities,
                _tickEveryFrameEntities,
                _spatialStaticEntities,
                _spatialDynamicEntities,
                _renderStaticEntities,
                _renderMaterialAnimatedEntities,
                _renderGeometryAnimatedEntities,
                _dynamicRenderEntities,
                _suspectCombinationCount);
        }
    }

    private void InternalAddEntities()
    {
        if (_baseObjectsToAdd.Count == 0)
        {
            return;
        }

        var entitiesToAdd = new List<Entity>(_baseObjectsToAdd);
        _baseObjectsToAdd.Clear();

        foreach (var entity in entitiesToAdd)
        {
            try
            {
                entity.Initialize();
                entity.InitializeWithWorld(this);
                MessageBus.RegisterEntity(entity);
                AddInSpacePartitioning(entity);
                _entities.Add(entity);
                SubscribeEntityTree(entity);
                Logs.WriteDebug($"Entity added : {entity.Name} {entity.Id}");
                NotifyEntityAddedRecursive(entity);
            }
            catch (Exception e)
            {
                Logs.WriteException(e);
            }
        }
    }

    private void AddInSpacePartitioning(Entity actor)
    {
        SpatialServices.WorldIndex.Add(actor, actor.GetBoundingBox());
        actor.ClearBoundingBoxDirty();
    }

    public void Draw(in RenderFrame frame)
    {
        CurrentRenderFrame = frame;

        try
        {
            var boundingFrustum = new BoundingFrustum(frame.ViewProjection);
            SpatialServices.WorldIndex.Query(boundingFrustum, _entitiesVisible);

            foreach (var entityBase in _entitiesVisible)
            {
                if (entityBase.RootComponent != null)
                {
                    entityBase.Draw(0f);
                }
            }

            if (DisplaySpacePartitioning)
            {
                SpatialServices.WorldIndex.DebugDraw(Game.Line3dRendererComponent);
            }
        }
        finally
        {
            _entitiesVisible.Clear();
            CurrentRenderFrame = null;
        }
    }

    public void QueryEntities(BoundingBox bounds, List<Entity> results, Func<Entity, bool> filter = null)
    {
        ArgumentNullException.ThrowIfNull(results);
        SpatialServices.WorldIndex.Query(bounds, results, filter);
    }

    public void RegisterWorldUI(WorldUIComponent worldUiComponent)
    {
        ArgumentNullException.ThrowIfNull(worldUiComponent);

        if (!_worldUiComponents.Contains(worldUiComponent))
        {
            _worldUiComponents.Add(worldUiComponent);
        }
    }

    public void UnregisterWorldUI(WorldUIComponent worldUiComponent)
    {
        ArgumentNullException.ThrowIfNull(worldUiComponent);
        _worldUiComponents.Remove(worldUiComponent);
    }

    public void DrawWorldUIToTextures()
    {
        foreach (var worldUiComponent in _worldUiComponents)
        {
            worldUiComponent.DrawToTexture();
        }
    }

    /// <summary>Registers a tile map surface so that its offscreen pass runs before the world draw.</summary>
    public void RegisterTileMapSurface(TileMapSurfaceComponent tileMapSurface)
    {
        ArgumentNullException.ThrowIfNull(tileMapSurface);

        if (!_tileMapSurfaces.Contains(tileMapSurface))
        {
            _tileMapSurfaces.Add(tileMapSurface);
        }
    }

    public void UnregisterTileMapSurface(TileMapSurfaceComponent tileMapSurface)
    {
        ArgumentNullException.ThrowIfNull(tileMapSurface);
        _tileMapSurfaces.Remove(tileMapSurface);
    }

    /// <summary>
    /// Runs the offscreen tile map passes. Called once per frame before the world is drawn so the
    /// produced textures are up to date when the quads consuming them are rendered.
    /// </summary>
    public void DrawTileMapSurfacesToTextures()
    {
        for (var index = 0; index < _tileMapSurfaces.Count; index++)
        {
            _tileMapSurfaces[index].DrawToTexture();
        }
    }

    /// <summary>
    /// Replaces the render frame seen by components during their draw and returns the previous value,
    /// so that an offscreen pass can install its own frame and restore the caller's one afterwards.
    /// </summary>
    internal RenderFrame? SetCurrentRenderFrame(RenderFrame? frame)
    {
        var previousFrame = CurrentRenderFrame;
        CurrentRenderFrame = frame;
        return previousFrame;
    }

    public void UpdateWorldUI(GameTime gameTime)
    {
        foreach (var worldUiComponent in _worldUiComponents)
        {
            worldUiComponent.Update(gameTime);
        }
    }

    public void OnScreenResized(int width, int height)
    {
        foreach (var entity in Entities)
        {
            entity.OnScreenResized(width, height);
        }
    }

    public override void Load(JObject element)
    {
        ClearEntities(true);
        base.Load(element);
        EnvironmentSettings.ResetToDefaults();

        foreach (var entityReferenceNode in element["entity_references"])
        {
            var entityReference = new EntityReference();
            entityReference.Load((JObject)entityReferenceNode);
            _entityReferences.Add(entityReference);
        }

        GameplayProxyClassName = element["script_class_name"].GetString();
        _gameplayProxyState = element["script"] as JObject;

        SpacePolicyName = element["space_policy"]?.Value<string>() ?? string.Empty;

        if (element.ContainsKey("game_mode_asset_id"))
        {
            PlayerStartupSettingsAssetId = element["game_mode_asset_id"].GetGuid();
        }

        if (element.ContainsKey("player_startup_settings_asset_id"))
        {
            PlayerStartupSettingsAssetId = element["player_startup_settings_asset_id"].GetGuid();
        }

        if (element.ContainsKey("gameplay_mode_asset_id"))
        {
            GameplayModeAssetId = element["gameplay_mode_asset_id"].GetGuid();
        }

        if (element["environment"] is JObject environmentNode)
        {
            EnvironmentSettings.Load(environmentNode);
        }
    }

    public bool IsPlayerController(Entity entity)
    {
        return GetPlayerController(entity) != null;
    }

    public PlayerController GetPlayerController(Entity entity)
    {
        foreach (var playerController in _playerControllers)
        {
            if (playerController.Pawn == entity)
            {
                return playerController;
            }
        }

        return null;
    }

    internal IEnumerable<ITransformableObject> GetTransformableObjects()
    {
        var selectables = new List<ITransformableObject>();

        foreach (var entity in _entities)
        {
            AddSelectablesFromActor(entity, selectables);
        }

        return selectables;
    }

    private void AddSelectablesFromActor(Entity actor, List<ITransformableObject> selectables)
    {
        if (actor.RootComponent != null)
        {
            selectables.Add(actor.RootComponent);

            foreach (var actorComponent in actor.Components)
            {
                if (actorComponent is SceneComponent sceneComponent)
                {
                    selectables.Add(sceneComponent);
                }
            }
        }

        foreach (var child in actor.Children)
        {
            AddSelectablesFromActor(child, selectables);
        }
    }

    internal void AddEntityReferenceImmediate(EntityReference entityReference, Entity entity)
    {
        _entityReferences.Add(entityReference);
        entity.Initialize();
        _entities.Add(entity);
        entity.InitializeWithWorld(this);
        MessageBus.RegisterEntity(entity);
        AddInSpacePartitioning(entity);
        SubscribeEntityTree(entity);
        NotifyEntityAddedRecursive(entity);
    }

    internal void RemoveEntityImmediate(Entity entity)
    {
        var entitiesToRemove = new List<EntityReference>();

        foreach (var entityReference in _entityReferences)
        {
            if (entityReference.Entity.Id == entity.Id)
            {
                entitiesToRemove.Add(entityReference);
                break;
            }
        }

        foreach (var entityReference in entitiesToRemove)
        {
            _entityReferences.Remove(entityReference);
        }

        MessageBus.UnregisterEntity(entity);
        UnsubscribeEntityTree(entity);
        _entities.Remove(entity);
        SpatialServices.WorldIndex.Remove(entity);
        entity.Destroy();
        NotifyEntityRemovedRecursive(entity);
    }

    private void SubscribeEntityTree(Entity entity)
    {
        if (!_observedEntities.Add(entity))
        {
            return;
        }

        entity.ChildAdded += OnEntityChildAdded;
        entity.ChildRemoved += OnEntityChildRemoved;

        foreach (var child in entity.Children)
        {
            SubscribeEntityTree(child);
        }
    }

    private void UnsubscribeEntityTree(Entity entity)
    {
        if (!_observedEntities.Remove(entity))
        {
            return;
        }

        entity.ChildAdded -= OnEntityChildAdded;
        entity.ChildRemoved -= OnEntityChildRemoved;

        foreach (var child in entity.Children)
        {
            UnsubscribeEntityTree(child);
        }
    }

    private void OnEntityChildAdded(object sender, Entity child)
    {
        child.Initialize();
        child.InitializeWithWorld(this);
        SubscribeEntityTree(child);
        NotifyEntityAddedRecursive(child);
    }

    private void OnEntityChildRemoved(object sender, Entity child)
    {
        UnsubscribeEntityTree(child);
        NotifyEntityRemovedRecursive(child);
    }

    private void NotifyEntityAddedRecursive(Entity entity)
    {
        EntityAdded?.Invoke(this, entity);

        foreach (var child in entity.Children)
        {
            NotifyEntityAddedRecursive(child);
        }
    }

    private void NotifyEntityRemovedRecursive(Entity entity)
    {
        EntityRemoved?.Invoke(this, entity);

        foreach (var child in entity.Children)
        {
            NotifyEntityRemovedRecursive(child);
        }
    }
}