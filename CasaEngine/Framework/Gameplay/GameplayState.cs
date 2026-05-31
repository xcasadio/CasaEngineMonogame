namespace CasaEngine.Framework.Gameplay;

public sealed class GameplayState
{
    public GameplayPhase Phase { get; set; } = GameplayPhase.NotStarted;
    public GameplayResult Result { get; set; } = GameplayResult.Running;
    public float ElapsedTime { get; set; }
    public int Score { get; set; }
    public int Lives { get; set; }
}