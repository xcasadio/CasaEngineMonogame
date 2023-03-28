using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Gameplay.Actor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = System.Text.Json.JsonSerializer;

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
    public Genbox.VelcroPhysics.Dynamics.World? Physic2dWorld { get; }

    public World(bool usePhysics = true)
    {
        if (usePhysics)
        {
            Physic2dWorld = new Genbox.VelcroPhysics.Dynamics.World(EngineComponents.Physics2dSettings.Gravity);
        }
    }

    public void AddObjectImmediately(Entity entity)
    {
        _entities.Add(entity);
#if EDITOR
        EntitiesChanged?.Invoke(this, EventArgs.Empty);
#endif
    }

    public void AddObject(Entity entity)
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

    public void Initialize()
    {
        foreach (var baseObject in _entities)
        {
            baseObject.Initialize();
        }

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
        Physic2dWorld?.Step(elapsedTime);

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
        foreach (var baseObject in _entities)
        {
            baseObject.Draw();
        }
    }

    public void Load(string fileName)
    {
        Clear();

        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));

        var rootElement = jsonDocument.RootElement;
        var version = rootElement.GetJsonPropertyByName("Version").Value.GetInt32();

        Name = rootElement.GetJsonPropertyByName("Name").Value.GetString();

        _entities.AddRange(EntityLoader.LoadFromArray(rootElement.GetJsonPropertyByName("Entities").Value));
    }

#if EDITOR
    public void Save(string path)
    {
        var fullFileName = Path.Combine(path, Name + ".json");

        JObject worldJson = new();
        base.Save(worldJson);
        var entitiesJArray = new JArray();

        foreach (var entity in Entities)
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