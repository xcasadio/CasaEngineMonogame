using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.World
{
    public class World
    {
        private readonly List<Entity> _baseObjects = new();
        private readonly List<Entity> _baseObjectsToAdd = new();

        public event EventHandler? Initializing;
        public event EventHandler? LoadingContent;
        public event EventHandler? Starting;

        public string Name { get; set; }
        public Entity[] BaseObjects => _baseObjects.ToArray();

        public Genbox.VelcroPhysics.Dynamics.World? PhysicWorld;

        public World(bool usePhysics = true)
        {
            if (usePhysics)
            {
                PhysicWorld = new Genbox.VelcroPhysics.Dynamics.World(Game.Engine.Instance.PhysicsSettings.Gravity);
            }
        }

        public void AddObjectImmediately(Entity entity)
        {
            _baseObjects.Add(entity);
        }

        public void AddObject(Entity entity)
        {
            _baseObjectsToAdd.Add(entity);
        }

        public void Clear()
        {
            _baseObjects.Clear();
            _baseObjectsToAdd.Clear();
        }

        public void Initialize()
        {
            foreach (var baseObject in _baseObjects)
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

        public virtual void Update(float elapsedTime)
        {
            PhysicWorld?.Step(elapsedTime);

            var toRemove = new List<Entity>();

            _baseObjects.AddRange(_baseObjectsToAdd);
            _baseObjectsToAdd.Clear();

            foreach (var a in _baseObjects)
            {
                a.Update(elapsedTime);

                if (a.ToBeRemoved)
                {
                    toRemove.Add(a);
                }
            }

            foreach (var a in toRemove)
            {
                _baseObjects.Remove(a);
            }
        }

        public virtual void Draw(float elapsedTime)
        {
            foreach (var baseObject in _baseObjects)
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

            _baseObjects.AddRange(EntityLoader.LoadFromArray(rootElement.GetJsonPropertyByName("Entities").Value));
        }
    }
}