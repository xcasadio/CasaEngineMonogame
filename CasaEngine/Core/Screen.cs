using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Core
{
    public class Screen
    {
        public int Width => GraphicsDevice.PresentationParameters.BackBufferWidth;

        public int Height => GraphicsDevice.PresentationParameters.BackBufferHeight;

        private GraphicsDevice GraphicsDevice { get; set; }

        public event EventHandler? ScreenSizeChanged;

        public Screen(GraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice;
        }

        internal void OnScreenSizeChanged(object sender, EventArgs e)
        {
            ScreenSizeChanged?.Invoke(sender, EventArgs.Empty);
        } // OnScreenSizeChanged
    } // Screen
}
