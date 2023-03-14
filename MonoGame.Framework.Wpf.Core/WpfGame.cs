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

        public bool FocusOnMouseOver { get; set; } = true;


        public ContentManager Content
        {
            get => _content;
            set => _content = value ?? throw new ArgumentNullException();
        }

        protected virtual void Draw(GameTime gameTime)
        {
        }

        protected override void Initialize()
        {
            base.Initialize();
            Content = new ContentManager(Services, _contentDir);
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

        }
    }
}