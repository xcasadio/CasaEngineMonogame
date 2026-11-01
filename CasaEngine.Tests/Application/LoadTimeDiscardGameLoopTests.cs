using CasaEngine.Framework.Application;
using CasaEngine.Tests.UI.Backend;
using Xunit;

namespace CasaEngine.Tests.Application;

/// <summary>
/// <see cref="LoadTimeDiscard"/> against the real <see cref="Microsoft.Xna.Framework.Game.Tick"/> of the MonoGame package:
/// it relies on a <see cref="Microsoft.Xna.Framework.Game.ResetElapsedTime"/> made from inside an Update ending the
/// catch-up of the running Tick, which only the package decides. Replays the four delays a world start was measured to
/// have, in order: the load, the first Update, the first Draw, and the first Update that receives time.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class LoadTimeDiscardGameLoopTests
{
    // Each delay is worth several steps of 1/60 s, so that catching one up cannot pass for a late Tick.
    private const int DelayMilliseconds = 120;
    private const int FramesToRun = 4;

    // The loop can run one Update more on a busy machine: Thread.Sleep overshoots.
    private const int MostUpdatesOfAFrameOnTime = 2;
    private const int FewestUpdatesOfACatchUp = 5;

    [GpuFact]
    public void WithoutTheDiscard_TheLoopCatchesUpTheStartOfTheWorld()
    {
        var updates = UpdatesPerFrame(discard: false);

        Assert.InRange(updates[0], FewestUpdatesOfACatchUp, int.MaxValue);
    }

    [GpuFact]
    public void WithTheDiscard_NoFrameCatchesUp()
    {
        var updates = UpdatesPerFrame(discard: true);

        Assert.Equal(FramesToRun, updates.Count);
        Assert.All(updates, count => Assert.InRange(count, 1, MostUpdatesOfAFrameOnTime));
    }

    private static List<int> UpdatesPerFrame(bool discard)
    {
        var host = GpuDeviceHost.Instance;

        return host.Invoke(() =>
        {
            var game = host.Game;
            var loadTime = discard ? new LoadTimeDiscard(game.ResetElapsedTime) : null;
            var updatesPerFrame = new List<int>();
            int updates = 0;

            bool wasFixedTimeStep = game.IsFixedTimeStep;
            var inactiveSleepTime = game.InactiveSleepTime;

            game.UpdateStarted = () =>
            {
                loadTime?.UpdateStarting();
                updates++;

                // The first Update, then the first Update that receives time.
                if (updates == 1 && updatesPerFrame.Count < 2)
                {
                    Thread.Sleep(DelayMilliseconds);
                }
            };

            game.FramePresented = () =>
            {
                // The first Draw.
                if (updatesPerFrame.Count == 0)
                {
                    Thread.Sleep(DelayMilliseconds);
                }

                updatesPerFrame.Add(updates);
                updates = 0;
                loadTime?.FramePresented();
            };

            try
            {
                game.IsFixedTimeStep = true;

                // This window is never active: the sleep of an inactive game would make a Tick late on its own.
                game.InactiveSleepTime = TimeSpan.Zero;
                game.ResetElapsedTime();

                // The load, in the Update MonoGame runs before its loop. The gap after it is the window being shown.
                Thread.Sleep(DelayMilliseconds);
                loadTime?.WorldReady();
                Thread.Sleep(DelayMilliseconds);

                for (int i = 0; i < FramesToRun; i++)
                {
                    game.Tick();
                }

                return updatesPerFrame;
            }
            finally
            {
                game.UpdateStarted = null;
                game.FramePresented = null;
                game.IsFixedTimeStep = wasFixedTimeStep;
                game.InactiveSleepTime = inactiveSleepTime;
            }
        });
    }
}
