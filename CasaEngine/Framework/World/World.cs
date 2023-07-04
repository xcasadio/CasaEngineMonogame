using System.Text.Json;
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
    private readonly List<Entity> _entities = new();
    private readonly List<Entity> _baseObjectsToAdd = new();

    public event EventHandler? Initializing;
    public event EventHandler? LoadingContent;
    public event EventHandler? Starting;

#if EDITOR
    public event EventHandler? EntitiesChanged;
#endif

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

    public void Clear()
    {
        _entities.Clear();
        _baseObjectsToAdd.Clear();
#if EDITOR
        EntitiesChanged?.Invoke(this, EventArgs.Empty);
#endif
    }

    public void Initialize(CasaEngineGame game)
    {
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

    public void Load(string fileName)
    {
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        Load(jsonDocument.RootElement);
    }

    public override void Load(JsonElement element)
    {
        Clear();
        base.Load(element.GetProperty("asset"));
        var version = element.GetJsonPropertyByName("version").Value.GetInt32();
        _entities.AddRange(EntityLoader.LoadFromArray(element.GetJsonPropertyByName("entities").Value));

#if EDITOR
        EntitiesChanged?.Invoke(this, EventArgs.Empty);
#endif
    }

#if EDITOR
    public void Save(string path)
    {
        var relativePath = Name + Constants.FileNameExtensions.World;
        var fullFileName = Path.Combine(path, relativePath);
        FileName = relativePath;

        JObject worldJson = new();
        base.Save(worldJson);
        worldJson.Add("version", 1);
        var entitiesJArray = new JArray();

        foreach (var entity in Entities.Where(x => !x.IsTemporary))
        {
            JObject entityObject = new();
            entity.Save(entityObject);
            entitiesJArray.Add(entityObject);
        }

        worldJson.Add("entities", entitiesJArray);

        using StreamWriter file = File.CreateText(fullFileName);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        worldJson.WriteTo(writer);
    }
#endif
}