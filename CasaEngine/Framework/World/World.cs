using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
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

#if EDITOR
    public event EventHandler? EntitiesChanged;
#endif

    //public IList<EntityReference> EntityReferences => _entityReferences;
    public IList<Entity> Entities => _entities;
    public Vector2 Gravity2d { get; set; } = Vector2.UnitY * -9.81f;
    public Vector3 Gravity { get; set; } = Vector3.UnitY * -9.81f;

    public void AddEntityImmediately(Entity entity)
    {
        _entities.Add(entity);
#if EDITOR
        EntitiesChanged?.Invoke(this, EventArgs.Empty);
#endif
    }

    public void AddEntity(Entity entity)
    {
        _baseObjectsToAdd.Add(entity);
    }

    public void ClearEntities()
    {
        _entities.Clear();
        _baseObjectsToAdd.Clear();
#if EDITOR
        EntitiesChanged?.Invoke(this, EventArgs.Empty);
#endif
    }

    public void AddEntityReference(EntityReference entityReference)
    {
        _entityReferences.Add(entityReference);
    }

    public void RemoveEntityReference(EntityReference entityReference)
    {
        _entityReferences.Remove(entityReference);
    }

    public void ClearEntityReference()
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
            ClearEntities();

            foreach (var entityReference in _entityReferences)
            {
                EntityLoader.LoadFromEntityReference(entityReference, game.GameManager.AssetContentManager, game.GraphicsDevice);
                _entities.Add(entityReference.Entity);
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

#if EDITOR
        EntitiesChanged?.Invoke(this, EventArgs.Empty);
#endif
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

        foreach (var a in _entities)
        {
            a.Update(elapsedTime);

            if (a.ToBeRemoved)
            {
                toRemove.Add(a);
            }
        }

        foreach (var a in toRemove)
        {
            _entities.Remove(a);
        }
    }

    public void Draw(float elapsedTime)
    {
        foreach (var entity in _entities)
        {
            entity.Draw();
        }
    }

    public void Load(string fileName, SaveOption option)
    {
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        Load(jsonDocument.RootElement, option);
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        ClearEntities();
        ClearEntityReference();
        base.Load(element.GetProperty("asset"), option);
        //_entities.AddRange(EntityLoader.LoadFromArray(element.GetJsonPropertyByName("entities").Value, option));

        foreach (var entityReferenceNode in element.GetProperty("entity_references").EnumerateArray())
        {
            var entityReference = new EntityReference();
            entityReference.Load(entityReferenceNode, option);
            _entityReferences.Add(entityReference);
        }
    }

#if EDITOR
    public void Save(string path, SaveOption option)
    {
        var relativePath = AssetInfo.Name + Constants.FileNameExtensions.World;
        var fullFileName = Path.Combine(path, relativePath);
        AssetInfo.FileName = relativePath;

        JObject worldJson = new();
        base.Save(worldJson, option);
        var entitiesJArray = new JArray();

        foreach (var entityReference in _entityReferences)
        {
            entityReference.InitialCoordinates.CopyFrom(entityReference.Entity.Coordinates);
            //entityReference.Name = entityReference.Entity.Name;

            JObject entityObject = new();
            entityReference.Save(entityObject, option);
            entitiesJArray.Add(entityObject);
        }

        worldJson.Add("entity_references", entitiesJArray);

        using StreamWriter file = File.CreateText(fullFileName);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        worldJson.WriteTo(writer);
    }
#endif
}