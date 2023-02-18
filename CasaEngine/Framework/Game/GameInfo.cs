namespace CasaEngine.Framework.Game
{
    public class GameInfo
    {
        public static GameInfo Instance { get; } = new();

        public World.World CurrentWorld { get; set; }
    }
}
