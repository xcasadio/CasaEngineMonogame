using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.World
{
    public class World
    {
        private readonly List<Entity> _entities = new();
        private readonly List<Entity> _baseObjectsToAdd = new();

        public event EventHandler? Initializing;
        public event EventHandler? LoadingContent;
        public event EventHandler? Starting;

#if EDITOR
        public event EventHandler? EntitiesChanged;
#endif

        public string Name { get; set; }
        public IList<Entity> Entities => _entities;
        public Genbox.VelcroPhysics.Dynamics.World? Physic2dWorld { get; }

        public World(bool usePhysics = true)
        {
            if (usePhysics)
            {
                Physic2dWorld = new Genbox.VelcroPhysics.Dynamics.World(Game.EngineComponents.Physics2dSettings.Gravity);
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

        public virtual void Update(float elapsedTime)
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

        public virtual void Draw(float elapsedTime)
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
    }
}