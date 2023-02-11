using CasaEngine.Front_End;
using CasaEngine.Game;
using CasaEngine.Gameplay;
using CasaEngine.Gameplay.Actor;
using CasaEngine.Physics2D;
using CasaEngineCommon.Design;

namespace CasaEngine.World
{
    public class World
    {

        private readonly List<Actor2D> _actors = new(30);
        private readonly List<Actor2D> _actorsToAdd = new();

        private readonly FarseerPhysics.Dynamics.World _physicWorld;
        private HudBase _hud = null;

        public event EventHandler Initializing;
        public event EventHandler LoadingContent;
        public event EventHandler Starting;

        public Actor2D[] Actors => _actors.ToArray();

        public FarseerPhysics.Dynamics.World PhysicWorld => _physicWorld;

        public HudBase Hud
        {
            get => _hud;
            set => _hud = value;
        }

        public World(bool usePhysics = true)
        {
            if (usePhysics == true)
            {
                _physicWorld = new FarseerPhysics.Dynamics.World(GameInfo.Instance.WorldInfo.WorldGravity);
            }
        }

        public void AddObject(Actor2D actor2D)
        {
            _actors.Add(actor2D);
        }

        public void PushObject(Actor2D actor2D)
        {
            _actorsToAdd.Add(actor2D);
        }

        public void Initialize()
        {

            if (Initializing != null)
            {
                Initializing.Invoke(this, EventArgs.Empty);
            }
        }

        public void LoadContent()
        {
            if (_hud != null)
            {
                //_HUD.LoadContent(GraphicsDevice);
            }

            if (LoadingContent != null)
            {
                LoadingContent.Invoke(this, EventArgs.Empty);
            }
        }

        public void Start()
        {
            if (_hud != null)
            {
                _hud.Start();
            }

            if (Starting != null)
            {
                Starting.Invoke(this, EventArgs.Empty);
            }
        }

        public virtual void Update(float elapsedTime)
        {
            List<Actor2D> toRemove = new List<Actor2D>();

            foreach (Actor2D a in _actorsToAdd)
            {
                _actors.Add(a);
            }

            _actorsToAdd.Clear();

            GameInfo.Instance.WorldInfo.Update(elapsedTime);

            if (_physicWorld != null)
            {
                _physicWorld.Step(elapsedTime);
            }

            foreach (Actor2D a in _actors)
            {
                a.Update(elapsedTime);

                if (a.Remove == true)
                {
                    toRemove.Add(a);
                }
            }

            foreach (Actor2D a in toRemove)
            {
                _actors.Remove(a);
            }

            if (_hud != null)
            {
                _hud.Update(elapsedTime);
            }

            Collision2DManager.Instance.Update();
        }

        public virtual void Draw(float elapsedTime)
        {
            foreach (Actor2D a in _actors)
            {
                if (a is IRenderable)
                {
                    ((IRenderable)a).Draw(elapsedTime);
                }
            }

            if (_hud != null)
            {
                _hud.Draw(elapsedTime);
            }
        }

    }
}
