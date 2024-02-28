
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Log;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.GameFramework;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Objects;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.SpacePartitioning.Octree;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
#if EDITOR
using XNAGizmo;
#endif

namespace CasaEngine.Framework.World;

public sealed class World : ObjectBase
{
    private readonly List<EntityReference> _entityReferences = new();
    private readonly List<Entity> _entities = new();
    private readonly List<Entity> _baseObjectsToAdd = new();
    private readonly List<ScreenGui> _screens = new();

    private readonly Octree<Entity> _octree;
    private readonly List<Entity> _entitiesVisible = new(1000);

    private readonly List<PlayerController> _playerControllers = new();

    public CasaEngineGame Game { get; private set; }
    public IList<Entity> Entities => _entities;
    public IEnumerable<ScreenGui> Screens => _screens;
    public string GameplayProxyClassName { get; set; }
    public GameplayProxy? GameplayProxy { get; private set; }
    public Guid GameModeAssetId { get; set; } = Guid.Empty;
    public GameMode GameMode { get; private set; }

    //UGameViewportClient ViewportClient
    public bool DisplaySpacePartitioning { get; set; }

    public World()
    {
        _octree = new Octree<Entity>(new BoundingBox(Vector3.One * -100000, Vector3.One * 100000), 64);
    }

    public void Clear()
    {
        ClearEntities(true);
        ClearScreens();
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

    public void ClearEntities(bool clearReferences = false)
    {
        foreach (var entity in _entities)
        {
            entity.Destroy();
        }

        _entities.Clear();
        _baseObjectsToAdd.Clear();
        _octree.Clear();

        if (clearReferences)
        {
            _entityReferences.Clear();
        }

#if EDITOR
        EntitiesClear?.Invoke(this, EventArgs.Empty);
#endif
    }

    public void LoadContent(CasaEngineGame game)
    {
        Game = game;
        LoadContent(AssetCatalog.IsLoaded);
    }

    private void LoadContent(bool withReference)
    {
        if (GameModeAssetId != Guid.Empty)
        {
            GameMode = Game.AssetContentManager.Load<GameMode>(GameModeAssetId);
        }
        else // TODO remove this
        {
            GameMode = new GameMode();
        }

        GameMode.InitGame(this);
        GameMode.MatchState = GameMode.EnteringMap;

        if (withReference)
        {
            foreach (var entityReference in _entityReferences)
            {
                LoadFromEntityReference(entityReference);
            }
        }

        //after all entities are added
        InternalAddEntities();

#if EDITOR
        if (!Game.IsRunningInGameEditorMode)
#endif
        {
            InitializePlayerControllers();
        }

#if !EDITOR
        //TODO : remove this, use a script to set active camera
        var camera = _entities.Select(x => x.GetComponent<CameraComponent>()).FirstOrDefault(x => x != null);

        if (camera == null)
        {
            camera = CreateDefaultCamera();
            Logs.WriteWarning($"No camera found in the world {Name}. Create default one");
        }

        Game.GameManager.ActiveCamera = camera;
#endif

        if (!string.IsNullOrWhiteSpace(GameplayProxyClassName))
        {
            GameplayProxy = ElementFactory.Create<GameplayProxy>(GameplayProxyClassName);
        }

#if EDITOR
        if (Game.IsRunningInGameEditorMode)
#endif
        {
            GameplayProxy?.Initialize(null);
            GameplayProxy?.InitializeWithWorld(this);
        }
    }

    private void InitializePlayerControllers()
    {
        if (GameMode.DefaultPawnAssetId == Guid.Empty)
        {
            return;
        }

        var pawn = SpawnEntity<Pawn>(GameMode.DefaultPawnAssetId);
        var playerController = ElementFactory.Create<PlayerController>(GameMode.PlayerControllerClass);
        playerController.Pawn = pawn;
        playerController.Player = new LocalPlayer(); // TODO
        _playerControllers.Add(playerController);

        var playerStartComponent = GetPlayerStart((int)PlayerIndex.One);
        if (playerStartComponent != null)
        {
            pawn.RootComponent?.Coordinates.CopyFrom(playerStartComponent.Coordinates);
        }

        InternalAddEntities();
    }

    private CameraComponent CreateDefaultCamera()
    {
        var entityCamera = new Entity();
        var camera = new CameraLookAtComponent();
        entityCamera.RootComponent = camera;
        camera.SetPositionAndTarget(Vector3.Forward * -10, Vector3.Zero);

        entityCamera.Initialize();
        entityCamera.InitializeWithWorld(this);

        return camera;
    }

    private void LoadFromEntityReference(EntityReference entityReference)
    {
        entityReference.Load(Game.AssetContentManager);
        AddEntity(entityReference.Entity);
    }

    public void BeginPlay()
    {
#if EDITOR
        if (Game.IsRunningInGameEditorMode)
        {
            return;
        }
#endif
        GameMode.StartPlay();

        GameplayProxy?.OnBeginPlay(this);

        foreach (var entity in _entities)
        {
            entity.GameplayProxy?.OnBeginPlay(this);
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
        GameMode.Tick(elapsedTime);

        if (GameMode.HasMatchEnded())
        {
            //??
        }

        var toRemove = new List<Entity>();

        InternalAddEntities();

        foreach (var entity in _entities)
        {
            if (entity.ToBeRemoved)
            {
                toRemove.Add(entity);
                _octree.RemoveItem(entity);
            }
            else
            {
                entity.Update(elapsedTime);

                if (IsBoundingBoxDirty(entity))
                {
                    _octree.MoveItem(entity, entity.GetBoundingBox());
                }
            }
        }

        foreach (var entity in toRemove)
        {
            _entities.Remove(entity);
        }

        _octree.ApplyPendingMoves();

#if EDITOR
        if (!Game.IsRunningInGameEditorMode)
        {
            GameplayProxy?.Update(elapsedTime);
        }
#else
        GameplayProxy?.Update(elapsedTime);
#endif

        foreach (var screen in _screens)
        {
            screen.Update(elapsedTime);
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

    private void InternalAddEntities()
    {
        var entitiesToAdd = new List<Entity>(_baseObjectsToAdd);
        _baseObjectsToAdd.Clear();

        foreach (var entity in entitiesToAdd)
        {
            entity.Initialize();
            entity.InitializeWithWorld(this);
            AddInSpacePartitioning(entity);
            Logs.WriteDebug($"Entity added : {entity.Name} {entity.Id}");
#if EDITOR
            EntityAdded?.Invoke(this, entity);
#endif
        }

        _entities.AddRange(entitiesToAdd);
    }

    private void AddInSpacePartitioning(Entity actor)
    {
        _octree.AddItem(actor.GetBoundingBox(), actor);
    }

    public void Draw(Matrix viewProjection)
    {
        var boundingFrustum = new BoundingFrustum(viewProjection);
        _octree.GetContainedObjects(boundingFrustum, _entitiesVisible);

        foreach (var entityBase in _entitiesVisible)
        {
            if (entityBase.RootComponent != null)
            {
                entityBase.Draw(0f);
            }
        }
        /*
        foreach (var entity in _entities)
        {
            entity.Draw(0f);
        }*/

        _entitiesVisible.Clear();

        if (DisplaySpacePartitioning)
        {
            OctreeVisualizer.DisplayBoundingBoxes(_octree, Game.Line3dRendererComponent);
        }

        foreach (var screen in _screens)
        {
            screen.Draw();
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
        ClearScreens();
        base.Load(element);

        foreach (var entityReferenceNode in element["entity_references"])
        {
            var entityReference = new EntityReference();
            entityReference.Load((JObject)entityReferenceNode);
            _entityReferences.Add(entityReference);
        }

        GameplayProxyClassName = element["script_class_name"].GetString();

        if (element.ContainsKey("game_mode_asset_id"))
        {
            GameModeAssetId = element["game_mode_asset_id"].GetGuid();
        }
    }

    public void AddScreen(ScreenGui screenGui)
    {
        _screens.Add(screenGui);

        foreach (var control in screenGui.Controls)
        {
            if (!control.Initialized)
            {
                control.Initialize(Game.UiManager);
            }
            Game.UiManager.Add(control);
        }
    }

    public void RemoveScreen(ScreenGui screenGui)
    {
        _screens.Remove(screenGui);

        foreach (var control in screenGui.Controls)
        {
            Game.UiManager.Remove(control);
        }
    }

    public void ClearScreens()
    {
        foreach (var screen in _screens)
        {
            foreach (var control in screen.Controls)
            {
                Game.UiManager.Remove(control);
            }
        }

        _screens.Clear();
    }

    public bool IsPlayerController(Entity entity)
    {
        return GetPlayerController(entity) != null;
    }

    public PlayerController? GetPlayerController(Entity entity)
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

#if EDITOR

    public event EventHandler? EntitiesClear;
    public event EventHandler<Entity> EntityAdded;
    public event EventHandler<Entity> EntityRemoved;

    public IEnumerable<ITransformable> GetSelectableComponents()
    {
        var selectables = new List<ITransformable>();

        foreach (var entity in _entities)
        {
            AddSelectablesFromActor(entity, selectables);
        }

        return selectables;
    }

    private void AddSelectablesFromActor(Entity actor, List<ITransformable> selectables)
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

    public void AddEntityWithEditor(Entity entity)
    {
        var entityReference = new EntityReference();
        entityReference.Name = entity.Name;
        entityReference.Entity = entity;

        AddEntityReferenceWithEditor(entityReference, entityReference.Entity);
    }

    public void AddEntityReference(EntityReference entityReference)
    {
        AddEntityReferenceWithEditor(entityReference, entityReference.Entity);
    }

    private void AddEntityReferenceWithEditor(EntityReference entityReference, Entity entity)
    {
        _entityReferences.Add(entityReference);
        _entities.Add(entity);
        entity.InitializeWithWorld(this);
        AddInSpacePartitioning(entity);

        EntityAdded?.Invoke(this, entity);
    }

    public void RemoveEntityWithEditor(Entity entity)
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

        _entities.Remove(entity);
        _octree.RemoveItem(entity);
        entity.Destroy();

        EntityRemoved?.Invoke(this, entity);
    }

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        var entitiesJArray = new JArray();

        foreach (var entityReference in _entityReferences)
        {
            if (entityReference.Entity.RootComponent != null)
            {
                entityReference.InitialCoordinates.CopyFrom(entityReference.Entity.RootComponent?.Coordinates);
            }
            //entityReference.Name = entityReference.Entity.Name;

            JObject entityObject = new();
            entityReference.Save(entityObject);
            entitiesJArray.Add(entityObject);
        }

        jObject.Add("entity_references", entitiesJArray);

        jObject.Add("script_class_name", GameplayProxyClassName);

        jObject.Add("game_mode_asset_id", GameModeAssetId);
    }

#endif
}