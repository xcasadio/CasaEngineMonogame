namespace Microsoft.Xna.Framework
{
    /// <summary>
    /// WpfGameComponent
    /// </summary>
    /// <seealso cref="Microsoft.Xna.Framework.IGameComponent" />
    /// <seealso cref="Microsoft.Xna.Framework.IUpdateable" />
    public class WpfGameComponent : IGameComponent, IUpdateable
    {
        bool _enabled = true;
        int _updateOrder;

        public WpfGameComponent(WpfGame game) => Game = game;

        public event EventHandler<EventArgs> EnabledChanged;

        public event EventHandler<EventArgs> UpdateOrderChanged;

        public WpfGame Game { get; }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                {
                    return;
                }

                _enabled = value;
                var ev = EnabledChanged;
                ev?.Invoke(this, EventArgs.Empty);
            }
        }

        public int UpdateOrder
        {
            get => _updateOrder;
            set
            {
                if (_updateOrder == value)
                {
                    return;
                }

                _updateOrder = value;
                var ev = UpdateOrderChanged;
                ev?.Invoke(this, EventArgs.Empty);
            }
        }

        public virtual void Initialize() { }

        public virtual void Update(GameTime gameTime) { }
    }
}