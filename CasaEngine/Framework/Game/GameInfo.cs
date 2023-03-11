using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.Framework.Game
{
    public class GameInfo
    {
        private World.World? _currentWorld;

        public event EventHandler? ReadyToStart;

        public static GameInfo Instance { get; } = new();

        public World.World? CurrentWorld
        {
            get => _currentWorld;
            set
            {
                _currentWorld = value;
                WorldChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public CameraComponent? ActiveCamera { get; set; }
        public event EventHandler? WorldChanged;

        public void InvokeReadyToStart(CasaEngineGame game)
        {
            ReadyToStart?.Invoke(game, EventArgs.Empty);
        }
    }
}
