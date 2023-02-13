using CasaEngine.World;

namespace CasaEngine.Game
{
    public class GameInfo
    {
        public static GameInfo Instance { get; } = new();

        public WorldInfo WorldInfo { get; } = new();
    }
}
