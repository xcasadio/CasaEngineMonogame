using CasaEngine.Entities;
using CasaEngine.Game;
using System.Text.Json;
using CasaEngine.Helpers;

namespace CasaEngine.World
{
    public class World
    {
        private readonly List<BaseObject> _baseObjects = new();
        private readonly List<BaseObject> _baseObjectsToAdd = new();

        public event EventHandler? Initializing;
        public event EventHandler? LoadingContent;
        public event EventHandler? Starting;

        public string Name { get; set; }
        public BaseObject[] BaseObjects => _baseObjects.ToArray();

        public FarseerPhysics.Dynamics.World? PhysicWorld;

        public World(bool usePhysics = true)
        {
            if (usePhysics)
            {
                PhysicWorld = new FarseerPhysics.Dynamics.World(Engine.Instance.PhysicsSettings.Gravity);
            }
        }

        public void AddObjectImmediately(BaseObject baseObject)
        {
            _baseObjects.Add(baseObject);
        }

        public void AddObject(BaseObject baseObject)
        {
            _baseObjectsToAdd.Add(baseObject);
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

            var toRemove = new List<BaseObject>();

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