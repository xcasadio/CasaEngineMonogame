using Microsoft.Xna.Framework.Content;

namespace Microsoft.Xna.Framework
{
    public abstract class WpfGame : D3D11Host
    {
        readonly string _contentDir;
        ContentManager _content;

        protected abstract bool CanRender { get; }

        public bool FocusOnMouseOver { get; set; } = true;

        protected WpfGame(string contentDir = "Content")
        {
            if (string.IsNullOrEmpty(contentDir))
            {
                throw new ArgumentNullException(nameof(contentDir));
            }

            _contentDir = contentDir;
            Focusable = true;
        }

        protected override void Dispose(bool disposing)
        {
            UnloadContent();
            Content?.Dispose();
        }

        public ContentManager Content
        {
            get => _content;
            set => _content = value ?? throw new ArgumentNullException();
        }

        protected sealed override void Render(GameTime time)
        {
            if (!CanRender)
            {
                return;
            }

            // just run as fast as possible, WPF itself is limited to 60 FPS so that's the max we will get
            Update(time);
            Draw(time);
        }

        protected override void Initialize()
        {
            base.Initialize();
            Content = new ContentManager(Services, _contentDir);
        }

        protected virtual void UnloadContent() { }
        protected virtual void Update(GameTime gameTime) { }
        protected virtual void Draw(GameTime gameTime) { }
    }
}