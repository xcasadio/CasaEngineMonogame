using System.Text.Json;
using CasaEngine.Core.Logs;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.SpacePartitioning.Octree;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.World;

public sealed class World : Asset
{
    private readonly List<EntityReference> _entityReferences = new();
    private readonly List<Entity> _entities = new();
    private readonly List<Entity> _baseObjectsToAdd = new();
    private readonly List<ScreenGui> _screens = new();

    private readonly Octree<EntityBase> _octree;
    private readonly List<EntityBase> _entitiesVisible = new(1000);

    public CasaEngineGame Game { get; }
    public IEnumerable<ScreenGui> Screens => _screens;
    public ExternalComponent? ExternalComponent { get; set; }
    public IList<Entity> Entities => _entities;
    public EntityBase RootEntity { get; }

    public bool DisplaySpacePartitioning { get; set; }


    public World(CasaEngineGame game)
    {
        Game = game;
        RootEntity = new EntityBase { Name = "RootEntity" };

        _octree = new Octree<EntityBase>(new BoundingBox(Vector3.One * -100000, Vector3.One * 100000), 64);
    }

    public void AddEntity(Entity entity)
    {
        _baseObjectsToAdd.Add(entity);
    }

    public void RemoveEntity(Entity entity)
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

    public void ClearEntityReferences()
    {
        _entityReferences.Clear();
    }

    public void Initialize()
    {
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
                entityReference.Entity.Initialize(Game);
                AddEntity(entityReference.Entity);
            }
        }

        InternalAddEntities();

#if !EDITOR
        //TODO : remove this, use a script to set active camera
        var camera = _entities
            .Select(x => x.ComponentManager.Components.FirstOrDefault(y => y is CameraComponent) as CameraComponent)
            .FirstOrDefault(x => x != null);

        Game.GameManager.ActiveCamera = camera;
#endif
    }

    public void BeginPlay()
    {
#if EDITOR
        if (!Game.GameManager.IsRunningInGameEditorMode)
#endif
        {
            ExternalComponent?.OnBeginPlay(this);
        }

        foreach (var entity in _entities)
        {
            entity.OnBeginPlay(this);
        }
    }

    public void Update(float elapsedTime)
    {
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

                if (entity.IsBoundingBoxDirty)
                {
                    _octree.MoveItem(entity, entity.BoundingBox);
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

    private void InternalAddEntities()
    {
        foreach (var entityToAdd in _baseObjectsToAdd)
        {
            entityToAdd.Initialize(Game);
            _octree.AddItem(entityToAdd.BoundingBox, entityToAdd);
#if EDITOR
            EntityAdded?.Invoke(this, entityToAdd);
#endif
        }

        _entities.AddRange(_baseObjectsToAdd);
        _baseObjectsToAdd.Clear();
    }

    public void Draw(Matrix viewProjection)
    {
        var boundingFrustum = new BoundingFrustum(viewProjection);
        _octree.GetContainedObjects(boundingFrustum, _entitiesVisible);

        foreach (var entityBase in _entitiesVisible)
        {
            entityBase.Draw();
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
        ClearEntityReferences();
        base.Load(element.GetProperty("asset"), option);

        foreach (var entityReferenceNode in element.GetProperty("entity_references").EnumerateArray())
        {
            var entityReference = new EntityReference();
            entityReference.Load(entityReferenceNode, option);
            _entityReferences.Add(entityReference);
        }

        if (element.TryGetProperty("external_component", out var externalComponentNode))
        {
            ExternalComponent = GameSettings.ScriptLoader.Load(externalComponentNode);
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
    public event EventHandler<Entity> EntityAdded;
    public event EventHandler<Entity> EntityRemoved;

    public void AddEntityWithEditor(Entity entity)
    {
        entity.Initialize(Game);
        _entities.Add(entity);
        _octree.AddItem(entity.BoundingBox, entity);
    }

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        var entitiesJArray = new JArray();

        foreach (var entityReference in _entityReferences)
        {
            entityReference.InitialCoordinates.CopyFrom(entityReference.Entity.Coordinates);
            //entityReference.Name = entityReference.Entity.Name;

            JObject entityObject = new();
            entityReference.Save(entityObject, option);
            entitiesJArray.Add(entityObject);
        }

        jObject.Add("entity_references", entitiesJArray);

        var externalComponentNode = new JObject();
        externalComponentNode.Add("type", ExternalComponent?.ExternalComponentId ?? -1);
        jObject.Add("external_component", externalComponentNode);
    }

    public void AddEntityReference(EntityReference entityReference)
    {
        AddEntityEditorMode(entityReference, entityReference.Entity);
    }

    public void RemoveEntityReference(EntityReference entityReference)
    {
        _entityReferences.Remove(entityReference);
    }

    public void AddEntityEditorMode(Entity entity)
    {
        var entityReference = new EntityReference();
        entityReference.Name = entity.Name;
        entityReference.Entity = entity;

        AddEntityEditorMode(entityReference, entityReference.Entity);
    }

    private void AddEntityEditorMode(EntityReference entityReference, Entity entity)
    {
        _entityReferences.Add(entityReference);
        _entities.Add(entity);

        EntityAdded?.Invoke(this, entity);
    }

    public void RemoveEntityEditorMode(Entity entity)
    {
        var entityReference = new EntityReference();
        entityReference.Name = entity.Name;
        entityReference.Entity = entity;

        _entityReferences.Remove(entityReference);
        _entities.Remove(entity);

        EntityRemoved?.Invoke(this, entity);
    }

#endif
}