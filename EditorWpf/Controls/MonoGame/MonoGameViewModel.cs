using System;
using System.Windows;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace EditorWpf.Controls.MonoGame
{
    public class MonoGameViewModel : ViewModel, IMonoGameViewModel
    {
        private DateTime _needResizeLastTime;

        protected MonoGameViewModel()
        {
        }

        public void Dispose()
        {
            Content?.Dispose();
        }

        public IGraphicsDeviceService GraphicsDeviceService { get; set; }
        protected GraphicsDevice? GraphicsDevice => GraphicsDeviceService.GraphicsDevice;
        protected MonoGameServiceProvider Services { get; private set; }
        protected ContentManager Content { get; set; }

        public virtual void Initialize()
        {
            Services = new MonoGameServiceProvider();
            Services.AddService(GraphicsDeviceService);
            Content = new ContentManager(Services) { RootDirectory = "Content" };
        }

        public virtual void LoadContent() { }
        public virtual void UnloadContent() { }
        public virtual void Update(GameTime gameTime) { }
        public virtual void Draw(GameTime gameTime) { }
        public virtual void OnActivated(object sender, EventArgs args) { }
        public virtual void OnDeactivated(object sender, EventArgs args) { }
        public virtual void OnExiting(object sender, EventArgs args) { }

        public virtual void SizeChanged(object sender, SizeChangedEventArgs args)
        {
            if (GraphicsDevice != null)
            {
                var newWidth = (int)args.NewSize.Width;
                var newHeight = (int)args.NewSize.Height;

                DateTime dateTimeNow = DateTime.Now;
                TimeSpan span = dateTimeNow.Subtract(_needResizeLastTime);

                if (span.TotalSeconds >= 1.0
                    && (newWidth != GraphicsDevice.PresentationParameters.BackBufferWidth
                        || newHeight != GraphicsDevice.PresentationParameters.BackBufferHeight))
                {
                    _needResizeLastTime = dateTimeNow;
                    PresentationParameters pp = GraphicsDevice.PresentationParameters;
                    pp.BackBufferWidth = newWidth;
                    pp.BackBufferHeight = newHeight;
                    GraphicsDevice.Reset(pp);
                    //System.Diagnostics.Debug.WriteLine(Environment.TickCount.ToString() + " - GraphicsDevice.Reset()");
                    LoadContent();
                }
            }
        }
    }
}
