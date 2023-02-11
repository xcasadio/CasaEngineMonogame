using CasaEngine.World;

namespace CasaEngine.Game
{
    public class GameInfo
    {
        static public GameInfo Instance { get; } = new();

        public WorldInfo WorldInfo { get; } = new();
    }
}
