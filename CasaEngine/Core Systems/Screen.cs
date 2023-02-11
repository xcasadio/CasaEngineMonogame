using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.CoreSystems
{
    public class Screen
    {


        public int Width => GraphicsDevice.PresentationParameters.BackBufferWidth;

        public int Height => GraphicsDevice.PresentationParameters.BackBufferHeight;


        private GraphicsDevice GraphicsDevice { get; set; }



        public event EventHandler ScreenSizeChanged;



        public Screen(GraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice;
        }



        internal void OnScreenSizeChanged(object sender, EventArgs e)
        {
            if (ScreenSizeChanged != null)
                ScreenSizeChanged(sender, EventArgs.Empty);
        } // OnScreenSizeChanged


    } // Screen

}
