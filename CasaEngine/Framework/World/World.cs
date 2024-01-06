using System.Text.Json;
using CasaEngine.Core.Logs;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.Scripting;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.World;

public sealed class World : Asset
{
    private readonly List<EntityReference> _entityReferences = new();
    private readonly List<Entity> _entities = new();
    private readonly List<Entity> _baseObjectsToAdd = new();
    private readonly List<ScreenGui> _screens = new();

    public CasaEngineGame Game { get; }
    public IEnumerable<ScreenGui> Screens => _screens;
    public ExternalComponent? ExternalComponent { get; set; }
    public IGroup SceneRoot { get; } = new Group();
    public IList<Entity> Entities => _entities;
    public EntityBase RootEntity { get; }

    public World(CasaEngineGame game)
    {
        Game = game;
        RootEntity = new EntityBase { Name = "RootEntity" };
    }

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
                AddEntityImmediately(entityReference.Entity);
            }
        }

        foreach (var entity in _entities)
        {
            entity.Initialize(Game);
        }

        InitializeEntities(RootEntity);

#if !EDITOR
        //TODO : remove this, use a script tou set active camera
        var camera = _entities
            .Select(x => x.ComponentManager.Components.FirstOrDefault(y => y is CameraComponent) as CameraComponent)
            .FirstOrDefault(x => x != null);

        Game.GameManager.ActiveCamera = camera;
#endif
    }

    private void InitializeEntities(EntityBase? entityBase)
    {
        if (entityBase == null)
        {
            return;
        }

        entityBase.Initialize(Game);

        foreach (var child in entityBase.Children)
        {
            InitializeEntities(child);
        }
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

        foreach (var screen in _screens)
        {
            screen.Update(elapsedTime);
        }
    }

    public void Draw(float elapsedTime)
    {
        //foreach (var entity in _entities)
        //{
        //    entity.Draw();
        //}

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