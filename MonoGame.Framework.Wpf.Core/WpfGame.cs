using Microsoft.Xna.Framework.Content;

namespace Microsoft.Xna.Framework
{
    /// <summary>
    /// WpfGame
    /// </summary>
    /// <seealso cref="Microsoft.Xna.Framework.D3D11Host" />
    public abstract class WpfGame : D3D11Host
    {
        readonly string _contentDir;
        ContentManager _content;
        readonly List<IUpdateable> _sortedUpdateables;
        readonly List<IDrawable> _sortedDrawables;

        protected WpfGame(string contentDir = "Content")
        {
            if (string.IsNullOrEmpty(contentDir))
            {
                throw new ArgumentNullException(nameof(contentDir));
            }

            _contentDir = contentDir;
            Focusable = true;
            Components = new GameComponentCollection();
            _sortedDrawables = new List<IDrawable>();
            _sortedUpdateables = new List<IUpdateable>();
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var c in Components)
            {
                if (c is IDisposable disp)
                {
                    disp.Dispose();
                }
            }

            Components.ComponentAdded -= ComponentAdded;
            Components.ComponentRemoved -= ComponentRemoved;
            Components.Clear();
            UnloadContent();
            Content?.Dispose();
        }

        public bool FocusOnMouseOver { get; set; } = true;

        public GameComponentCollection Components { get; }

        public ContentManager Content
        {
            get => _content;
            set => _content = value ?? throw new ArgumentNullException();
        }

        protected virtual void Draw(GameTime gameTime)
        {
            foreach (var drawable in _sortedDrawables)
            {
                if (drawable.Visible)
                {
                    drawable.Draw(gameTime);
                }
            }
        }

        protected override void Initialize()
        {
            base.Initialize();
            Content = new ContentManager(Services, _contentDir);
            // hook events now (graphics, etc. is now loaded) any components added prior we insert manually
            foreach (var c in Components)
            {
                ComponentAdded(this, new GameComponentCollectionEventArgs(c));
            }

            Components.ComponentAdded += ComponentAdded;
            Components.ComponentRemoved += ComponentRemoved;
            LoadContent();
        }

        protected virtual void LoadContent() { }

        protected sealed override void Render(GameTime time)
        {
            // just run as fast as possible, WPF itself is limited to 60 FPS so that's the max we will get
            Update(time);
            Draw(time);
        }

        protected virtual void UnloadContent() { }

        protected virtual void Update(GameTime gameTime)
        {
            foreach (var updateable in _sortedUpdateables)
            {
                if (updateable.Enabled)
                {
                    updateable.Update(gameTime);
                }
            }
        }

        void ComponentRemoved(object sender, GameComponentCollectionEventArgs args)
        {
            if (args.GameComponent is IUpdateable update)
            {
                update.UpdateOrderChanged -= UpdateOrderChanged;
                _sortedUpdateables.Remove(update);
            }
            if (args.GameComponent is IDrawable draw)
            {
                draw.DrawOrderChanged -= DrawOrderChanged;
                _sortedDrawables.Remove(draw);
            }
        }

        void ComponentAdded(object sender, GameComponentCollectionEventArgs args)
        {
            // monogame also calls initialize
            // I would have assumed that there'd be some property IsInitialized to prevent multiple calls to Initialize, but there isn't
            args.GameComponent.Initialize();
            if (args.GameComponent is IUpdateable update)
            {
                _sortedUpdateables.Add(update);
                update.UpdateOrderChanged += UpdateOrderChanged;
                SortUpdatables();
            }
            if (args.GameComponent is IDrawable draw)
            {
                _sortedDrawables.Add(draw);
                draw.DrawOrderChanged += DrawOrderChanged;
                SortDrawables();
            }
        }

        void SortDrawables() => _sortedDrawables.Sort((a, b) => a.DrawOrder.CompareTo(b.DrawOrder));

        void DrawOrderChanged(object sender, EventArgs e) => SortDrawables();

        void UpdateOrderChanged(object sender, EventArgs eventArgs) => SortUpdatables();

        void SortUpdatables() => _sortedUpdateables.Sort((a, b) => a.UpdateOrder.CompareTo(b.UpdateOrder));
    }
}