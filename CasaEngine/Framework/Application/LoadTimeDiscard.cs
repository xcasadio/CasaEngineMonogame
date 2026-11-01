namespace CasaEngine.Framework.Application;

/// <summary>
/// Keeps the time a world takes to start off the game clock: a world's time begins at its first frame. Without this,
/// MonoGame replays that time, up to <c>Game.MaxElapsedTime</c>, as Updates with no Draw under a fixed step and as one
/// long Update under a variable step, and everything driven by the elapsed time, a screen fade first of all, ends
/// before anyone sees it.
/// <para/>
/// Resetting once when the world is ready is not enough, because the load is only the first delay. The first Update of
/// the views and the first Draw come after it, and the first Update that receives time is slow too, since what skips a
/// zero elapsed time, physics for one, does its first work there. So the time is dropped over two frames:
/// <list type="number">
/// <item>The first frame runs at time zero, so what follows the elapsed time shows where it started.</item>
/// <item>The second frame gets one normal step under a fixed step. Under a variable step, the default, it gets what
/// passed since the first frame was presented, a short gap. What it took is dropped once it is presented.</item>
/// </list>
/// A slow frame after those two is an ordinary late frame. <c>Game.ResetElapsedTime</c> leaves
/// <c>GameTime.TotalGameTime</c> alone, so what reads that clock, MGUI first of all, is not held back (ADR-0082).
/// <para/>
/// Knows no MonoGame type, like <see cref="Rendering.ScreenEffects.ScreenEffectService"/>: <see cref="CasaEngineGame"/>
/// drives it from the loop it owns. Under external view management that loop is not the game's, so it stays unused.
/// </summary>
internal sealed class LoadTimeDiscard
{
    private const int FramesToDiscard = 2;

    private readonly Action _resetElapsedTime;
    private int _framesLeft;

    /// <param name="resetElapsedTime">Empties the time the game loop has to catch up, and the elapsed time of the running Update.</param>
    public LoadTimeDiscard(Action resetElapsedTime)
    {
        _resetElapsedTime = resetElapsedTime ?? throw new ArgumentNullException(nameof(resetElapsedTime));
    }

    /// <summary>The world is ready, its first screen included. Its first frame is now due.</summary>
    public void WorldReady()
    {
        _framesLeft = FramesToDiscard;
        _resetElapsedTime();
    }

    /// <summary>
    /// To call first in Update. This is what stops the catch-up of the first loop iteration, and the first world needs
    /// it: MonoGame runs one Update before its loop starts, so that world is ready before any catch-up exists.
    /// </summary>
    public void UpdateStarting()
    {
        if (_framesLeft == FramesToDiscard)
        {
            _resetElapsedTime();
        }
    }

    /// <summary>To call once a frame is presented, so that what the Draw and the presentation took is dropped too.</summary>
    public void FramePresented()
    {
        if (_framesLeft > 0)
        {
            _framesLeft--;
            _resetElapsedTime();
        }
    }
}
