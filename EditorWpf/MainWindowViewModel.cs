using EditorWpf.Controls.MonoGame;
using Microsoft.Xna.Framework;

namespace EditorWpf
{
    public class MainWindowViewModel : MonoGameViewModel
    {
        public override void LoadContent()
        {

        }

        public override void Update(GameTime gameTime)
        {

        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
        }
    }
}