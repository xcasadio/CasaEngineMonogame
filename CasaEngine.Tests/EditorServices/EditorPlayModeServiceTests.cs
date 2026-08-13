using CasaEngine.EditorServices.PlayMode;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

public class EditorPlayModeServiceTests
{
    private sealed class RecordingController : IEditorPlaySessionController
    {
        public bool StartResult = true;
        public Exception? StartException;
        public int StartCount;
        public int StopCount;
        public readonly List<bool> PauseCalls = new();

        public bool StartSession()
        {
            StartCount++;
            if (StartException != null)
            {
                throw StartException;
            }

            return StartResult;
        }

        public void StopSession()
        {
            StopCount++;
        }

        public void SetPaused(bool paused)
        {
            PauseCalls.Add(paused);
        }
    }

    private static (EditorPlayModeService Service, RecordingController Controller, List<EditorPlayModeState> States) CreateService()
    {
        var controller = new RecordingController();
        var service = new EditorPlayModeService(controller);
        var states = new List<EditorPlayModeState>();
        service.StateChanged += (_, e) => states.Add(e.NewState);
        return (service, controller, states);
    }

    [Fact]
    public void InitialState_IsEditing()
    {
        var (service, _, _) = CreateService();

        Assert.Equal(EditorPlayModeState.Editing, service.State);
        Assert.False(service.IsPlaySessionActive);
    }

    [Fact]
    public void TryStartPlay_FromEditing_TransitionsThroughStartingToPlaying()
    {
        var (service, controller, states) = CreateService();

        Assert.True(service.TryStartPlay());

        Assert.Equal(EditorPlayModeState.Playing, service.State);
        Assert.True(service.IsPlaySessionActive);
        Assert.Equal(1, controller.StartCount);
        Assert.Equal(new[] { EditorPlayModeState.Starting, EditorPlayModeState.Playing }, states);
    }

    [Fact]
    public void TryStartPlay_WhenControllerRefuses_ReturnsToEditing()
    {
        var (service, controller, states) = CreateService();
        controller.StartResult = false;

        Assert.False(service.TryStartPlay());

        Assert.Equal(EditorPlayModeState.Editing, service.State);
        Assert.Equal(new[] { EditorPlayModeState.Starting, EditorPlayModeState.Editing }, states);
    }

    [Fact]
    public void TryStartPlay_WhenControllerThrows_ReturnsToEditingAndPropagates()
    {
        var (service, controller, _) = CreateService();
        controller.StartException = new InvalidOperationException("boom");

        Assert.Throws<InvalidOperationException>(() => service.TryStartPlay());
        Assert.Equal(EditorPlayModeState.Editing, service.State);
    }

    [Fact]
    public void TryStartPlay_WhenAlreadyPlaying_IsRefused()
    {
        var (service, controller, _) = CreateService();
        service.TryStartPlay();

        Assert.False(service.TryStartPlay());
        Assert.Equal(1, controller.StartCount);
    }

    [Fact]
    public void TryStopPlay_FromPlaying_StopsSessionAndReturnsToEditing()
    {
        var (service, controller, states) = CreateService();
        service.TryStartPlay();
        states.Clear();

        Assert.True(service.TryStopPlay());

        Assert.Equal(EditorPlayModeState.Editing, service.State);
        Assert.Equal(1, controller.StopCount);
        Assert.Equal(new[] { EditorPlayModeState.Stopping, EditorPlayModeState.Editing }, states);
    }

    [Fact]
    public void TryStopPlay_FromPaused_Works()
    {
        var (service, controller, _) = CreateService();
        service.TryStartPlay();
        service.TryPause();

        Assert.True(service.TryStopPlay());
        Assert.Equal(EditorPlayModeState.Editing, service.State);
        Assert.Equal(1, controller.StopCount);
    }

    [Fact]
    public void TryStopPlay_FromEditing_IsRefused()
    {
        var (service, controller, _) = CreateService();

        Assert.False(service.TryStopPlay());
        Assert.Equal(0, controller.StopCount);
    }

    [Fact]
    public void PauseAndResume_OnlyFromMatchingStates()
    {
        var (service, controller, _) = CreateService();

        Assert.False(service.TryPause());
        Assert.False(service.TryResume());

        service.TryStartPlay();

        Assert.False(service.TryResume());
        Assert.True(service.TryPause());
        Assert.Equal(EditorPlayModeState.Paused, service.State);
        Assert.False(service.TryPause());

        Assert.True(service.TryResume());
        Assert.Equal(EditorPlayModeState.Playing, service.State);
        Assert.Equal(new[] { true, false }, controller.PauseCalls);
    }

    [Fact]
    public void TogglePlayStop_AlternatesBetweenSessions()
    {
        var (service, controller, _) = CreateService();

        Assert.True(service.TogglePlayStop());
        Assert.Equal(EditorPlayModeState.Playing, service.State);

        Assert.True(service.TogglePlayStop());
        Assert.Equal(EditorPlayModeState.Editing, service.State);

        Assert.Equal(1, controller.StartCount);
        Assert.Equal(1, controller.StopCount);
    }

    [Fact]
    public void TogglePause_AlternatesOnlyDuringPlay()
    {
        var (service, _, _) = CreateService();

        Assert.False(service.TogglePause());

        service.TryStartPlay();
        Assert.True(service.TogglePause());
        Assert.Equal(EditorPlayModeState.Paused, service.State);
        Assert.True(service.TogglePause());
        Assert.Equal(EditorPlayModeState.Playing, service.State);
    }
}
