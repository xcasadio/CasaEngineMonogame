using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Logger;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.World;

public sealed class World : Asset
{
    private readonly List<EntityReference> _entityReferences = new();

    private readonly List<Entity> _entities = new();
    private readonly List<Entity> _baseObjectsToAdd = new();

    public event EventHandler? Initializing;
    public event EventHandler? LoadingContent;
    public event EventHandler? Starting;

    public IList<Entity> Entities => _entities;

    public void AddEntityImmediately(Entity entity)
    {
        _entities.Add(entity);
#if EDITOR
        EntityAdded?.Invoke(this, entity);
#endif
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
#if EDITOR
        EntitiesClear?.Invoke(this, EventArgs.Empty);
#endif
    }

    public void ClearEntityReferences()
    {
        _entityReferences.Clear();
    }

    public void Initialize(CasaEngineGame game)
    {
        Initialize(game, GameSettings.AssetInfoManager.IsLoaded);
    }

    private void Initialize(CasaEngineGame game, bool withReference)
    {
        if (withReference)
        {
#if EDITOR
            ClearEntities();
#endif

            foreach (var entityReference in _entityReferences)
            {
                EntityLoader.LoadFromEntityReference(entityReference, game.GameManager.AssetContentManager, game.GraphicsDevice);
                AddEntityImmediately(entityReference.Entity);
            }
        }

        foreach (var entity in _entities)
        {
            entity.Initialize(game);
        }

#if !EDITOR
        //TODO : remove this, use a script tou set active camera
        var camera = _entities
            .Select(x => x.ComponentManager.Components.FirstOrDefault(y => y is CameraComponent) as CameraComponent)
            .FirstOrDefault(x => x != null);

        game.GameManager.ActiveCamera = camera;
#endif

        Initializing?.Invoke(this, EventArgs.Empty);
    }

    public void LoadContent()
    {
        LoadingContent?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        Starting?.Invoke(this, EventArgs.Empty);
    }

    public void Update(float elapsedTime)
    {
        var toRemove = new List<Entity>();

        _entities.AddRange(_baseObjectsToAdd);
        _baseObjectsToAdd.Clear();

        foreach (var entity in _entities)
        {
            if (entity.ToBeRemoved)
            {
                toRemove.Add(entity);
            }
            else
            {
                entity.Update(elapsedTime);
            }
        }

        foreach (var entity in toRemove)
        {
            _entities.Remove(entity);
        }
    }

    public void Draw(float elapsedTime)
    {
        foreach (var entity in _entities)
        {
            entity.Draw();
        }
    }

    //TODO : remove it, use AssetContentManager
    public void Load(string fileName, SaveOption option)
    {
        LogManager.Instance.WriteLineInfo($"Load world {fileName}");
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
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
    }

#if EDITOR

    public event EventHandler? EntitiesClear;
    public event EventHandler<Entity> EntityAdded;
    public event EventHandler<Entity> EntityRemoved;

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