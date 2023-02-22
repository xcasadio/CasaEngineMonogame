using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Game
{
    public class CasaEngineGame : Microsoft.Xna.Framework.Game
    {
        private readonly GameManager _gameManager;

        public CasaEngineGame()
        {
            _gameManager = new GameManager(this);
        }

        protected override void Initialize()
        {
            _gameManager.Initialize();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _gameManager.BeginLoadContent();
            base.LoadContent();
            _gameManager.EndLoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            _gameManager.BeginUpdate(gameTime);
            base.Update(gameTime);
            _gameManager.EndUpdate(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            _gameManager.BeginDraw(gameTime);
            base.Draw(gameTime);
            _gameManager.EndDraw(gameTime);
        }
    }
}
