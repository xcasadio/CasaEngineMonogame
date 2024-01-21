using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logs;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.SpacePartitioning.Octree;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.World;

public sealed class World : Asset
{
    private readonly List<EntityReference> _entityReferences = new();
    private readonly List<AActor> _entities = new();
    private readonly List<AActor> _baseObjectsToAdd = new();
    private readonly List<ScreenGui> _screens = new();

    private readonly Octree<AActor> _octree;
    private readonly List<AActor> _entitiesVisible = new(1000);

    public CasaEngineGame Game { get; private set; }
    public IEnumerable<ScreenGui> Screens => _screens;
    public GameplayProxy? GameplayProxy { get; set; }
    public IList<AActor> Entities => _entities;

    public bool DisplaySpacePartitioning { get; set; }

    //UGameViewportClient ViewportClient


    public World()
    {
        _octree = new Octree<AActor>(new BoundingBox(Vector3.One * -100000, Vector3.One * 100000), 64);
    }

    public void AddEntity(AActor entity)
    {
        _baseObjectsToAdd.Add(entity);
    }

    public void RemoveEntity(AActor entity)
    {
        entity.Destroy();
    }

    public void ClearEntities()
    {
        _entities.Clear();
        _baseObjectsToAdd.Clear();
        _octree.Clear();

#if EDITOR
        EntitiesClear?.Invoke(this, EventArgs.Empty);
#endif
    }

    public void Initialize(CasaEngineGame game)
    {
        Game = game;
        Initialize(GameSettings.AssetInfoManager.IsLoaded);
    }

    private void Initialize(bool withReference)
    {
        if (withReference)
        {
#if EDITOR
            ClearEntities();
#endif

            foreach (var entityReference in _entityReferences)
            {
                EntityLoader.LoadFromEntityReference(entityReference, Game.GameManager.AssetContentManager);
                AddEntity(entityReference.Entity);
            }
        }

        InternalAddEntities();

#if !EDITOR
        //TODO : remove this, use a script to set active camera
        var camera = _entities.Select(x => x.GetComponent<CameraComponent>()).First(x => x != null);
        Game.GameManager.ActiveCamera = camera;
#endif
    }

    public void BeginPlay()
    {
#if EDITOR
        if (!Game.GameManager.IsRunningInGameEditorMode)
#endif
        {
            GameplayProxy?.OnBeginPlay(this);
        }

        foreach (var entity in _entities)
        {
            entity.GameplayProxy?.OnBeginPlay(this);
        }
    }

    public void Update(float elapsedTime)
    {
        var toRemove = new List<AActor>();

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
                    _octree.MoveItem(entity, GetBoundingBox(entity));
                }
            }
        }

        foreach (var entity in toRemove)
        {
            _entities.Remove(entity);
        }

        _octree.ApplyPendingMoves();

        foreach (var screen in _screens)
        {
            screen.Update(elapsedTime);
        }
    }

    private bool IsBoundingBoxDirty(AActor actor)
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

    private BoundingBox GetBoundingBox(AActor actor)
    {
        var boundingBox = actor.RootComponent?.BoundingBox ?? new BoundingBox();

        foreach (var component in actor.Components)
        {
            if (component is SceneComponent sceneComponent)
            {
                boundingBox.ExpandBy(sceneComponent.BoundingBox);
            }
        }

        return boundingBox;
    }

    private void InternalAddEntities()
    {
        foreach (var entityToAdd in _baseObjectsToAdd)
        {
            entityToAdd.Initialize();
            entityToAdd.InitializeWithWorld(this);
            AddInSpacePartitioning(entityToAdd);
#if EDITOR
            EntityAdded?.Invoke(this, entityToAdd);
#endif
        }

        _entities.AddRange(_baseObjectsToAdd);
        _baseObjectsToAdd.Clear();
    }

    private void AddInSpacePartitioning(AActor actor)
    {
        _octree.AddItem(GetBoundingBox(actor), actor);
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

        _entitiesVisible.Clear();

        if (DisplaySpacePartitioning)
        {
            OctreeVisualizer.DisplayBoundingBoxes(_octree, Game.GameManager.Line3dRendererComponent);
        }

        foreach (var screen in _screens)
        {
            screen.Draw();
        }
    }

    public void ScreenResized(int width, int height)
    {
        foreach (var entity in Entities)
        {
            entity.ScreenResized(width, height);
        }
    }

    //TODO : remove it, use AssetContentManager
    public void Load(string fileName, SaveOption option)
    {
        LogManager.Instance.WriteInfo($"Load world {fileName}");
        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, fileName);
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fullFileName));
        Load(jsonDocument.RootElement, option);
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        ClearEntities();
        _entityReferences.Clear();
        base.Load(element.GetProperty("asset"), option);

        foreach (var entityReferenceNode in element.GetProperty("entity_references").EnumerateArray())
        {
            var entityReference = new EntityReference();
            entityReference.Load(entityReferenceNode, option);
            _entityReferences.Add(entityReference);
        }

        if (element.TryGetProperty("external_component", out var externalComponentNode)
            && externalComponentNode.GetProperty("type").GetInt32() != IdManager.InvalidId)
        {
            System.Diagnostics.Debugger.Break();
            //GameplayProxy = GameSettings.ScriptLoader.Load(externalComponentNode);
        }
    }

    public void AddScreen(ScreenGui screenGui)
    {
        _screens.Add(screenGui);

        foreach (var control in screenGui.Controls)
        {
            Game.GameManager.UiManager.Add(control);
        }
    }

    public void RemoveScreen(ScreenGui screenGui)
    {
        _screens.Remove(screenGui);

        foreach (var control in screenGui.Controls)
        {
            Game.GameManager.UiManager.Remove(control);
        }
    }

    public void ClearScreens()
    {
        foreach (var screen in _screens)
        {
            foreach (var control in screen.Controls)
            {
                Game.GameManager.UiManager.Remove(control);
            }
        }

        _screens.Clear();
    }

#if EDITOR

    public event EventHandler? EntitiesClear;
    public event EventHandler<AActor> EntityAdded;
    public event EventHandler<AActor> EntityRemoved;

    public void AddEntityWithEditor(AActor entity)
    {
        entity.InitializeWithWorld(this);
        _entities.Add(entity);
        AddInSpacePartitioning(entity);
    }

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        var entitiesJArray = new JArray();

        foreach (var entityReference in _entityReferences)
        {
            if (entityReference.Entity.RootComponent != null)
            {
                entityReference.InitialCoordinates = new Coordinates(entityReference.Entity.RootComponent?.Coordinates);
            }
            //entityReference.Name = entityReference.Entity.Name;

            JObject entityObject = new();
            entityReference.Save(entityObject, option);
            entitiesJArray.Add(entityObject);
        }

        jObject.Add("entity_references", entitiesJArray);

        if (GameplayProxy == null)
        {
            jObject.Add("external_component", "null");
        }
        else
        {
            var externalComponentNode = new JObject { { "type", GameplayProxy.GetType().Name } };
            jObject.Add("external_component", externalComponentNode);
        }
    }

    public void AddEntityReference(EntityReference entityReference)
    {
        AddEntityEditorMode(entityReference, entityReference.Entity);
    }

    public void RemoveEntityReference(EntityReference entityReference)
    {
        _entityReferences.Remove(entityReference);
    }

    public void AddEntityEditorMode(AActor entity)
    {
        var entityReference = new EntityReference();
        entityReference.Name = entity.Name;
        entityReference.Entity = entity;

        AddEntityEditorMode(entityReference, entityReference.Entity);
    }

    private void AddEntityEditorMode(EntityReference entityReference, AActor entity)
    {
        _entityReferences.Add(entityReference);
        _entities.Add(entity);
        AddInSpacePartitioning(entity);

        EntityAdded?.Invoke(this, entity);
    }

    public void RemoveEntityEditorMode(AActor entity)
    {
        foreach (var entityReference in _entityReferences)
        {
            if (entityReference.AssetId == entity.Id)
            {
                _entityReferences.Remove(entityReference);
                break;
            }
        }

        _entities.Remove(entity);
        _octree.RemoveItem(entity);

        EntityRemoved?.Invoke(this, entity);
    }

#endif
}