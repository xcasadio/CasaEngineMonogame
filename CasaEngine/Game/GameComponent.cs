using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace CasaEngine.Game
{
    public class GameComponent
        : IGameComponent,
        IUpdateable,
        IDisposable
    {
        private bool enabled = true;
        private int updateOrder;
        private CustomGame game;

        public event EventHandler<EventArgs> EnabledChanged;
        public event EventHandler<EventArgs> UpdateOrderChanged;
        public event EventHandler<EventArgs> Disposed;

        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (enabled != value)
                {
                    enabled = value;
                    OnEnabledChanged(this, EventArgs.Empty);
                }
            }
        }

        public int UpdateOrder
        {
            get { return updateOrder; }
            set
            {
                if (updateOrder != value)
                {
                    updateOrder = value;
                    OnUpdateOrderChanged(this, EventArgs.Empty);
                }
            }
        }

        public CustomGame Game
        {
            get { return game; }
        }

        public GameComponent(CustomGame game)
        {
            this.game = game;
        }

        ~GameComponent()
        {
            this.Dispose(false);
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
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.Game != null)
                {
                    this.Game.Components.Remove(this);
                }
                if (this.Disposed != null)
                {
                    this.Disposed(this, EventArgs.Empty);
                }
            }
        }
    }
}
