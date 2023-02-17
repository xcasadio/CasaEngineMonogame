using Microsoft.Xna.Framework;

namespace CasaEngine.Game
{
    /*
    public class GameComponent : IGameComponent, IUpdateable, IDisposable
    {
        private bool _enabled = true;
        private int _updateOrder;
        private readonly CustomGame _game;

        public event EventHandler<EventArgs> EnabledChanged;
        public event EventHandler<EventArgs> UpdateOrderChanged;
        public event EventHandler<EventArgs> Disposed;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnEnabledChanged(this, EventArgs.Empty);
                }
            }
        }

        public int UpdateOrder
        {
            get => _updateOrder;
            set
            {
                if (_updateOrder != value)
                {
                    _updateOrder = value;
                    OnUpdateOrderChanged(this, EventArgs.Empty);
                }
            }
        }

        public CustomGame Game => _game;

        public GameComponent(CustomGame game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }
            _game = game;
        }

        ~GameComponent()
        {
            Dispose(false);
        }

        protected virtual void OnEnabledChanged(object sender, EventArgs args)
        {
            if (EnabledChanged != null)
            {
                EnabledChanged(sender, args);
            }
        }

        protected virtual void OnUpdateOrderChanged(object sender, EventArgs args)
        {
            if (UpdateOrderChanged != null)
            {
                UpdateOrderChanged(sender, args);
            }
        }

        public virtual void Initialize()
        {
        }

        public virtual void Update(GameTime gameTime)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Game != null)
                {
                    Game.Components.Remove(this);
                }
                if (Disposed != null)
                {
                    Disposed(this, EventArgs.Empty);
                }
            }
        }
    }*/
}
