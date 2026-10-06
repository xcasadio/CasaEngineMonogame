using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Spatial;
using Xunit;
using Vector3 = System.Numerics.Vector3;

namespace CasaEngine.Tests.Audio.Spatial;

/// <summary>
/// The listener stack of <see cref="AudioService"/> (plan T9.4, decision P35): the last registered listener is the active
/// one, a removal reactivates the previous one, and no listener means no spatialization. Log-capturing tests rely on the
/// process-global <see cref="Logs"/>, hence the serialized collection.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceListenerTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }

    private static AudioListenerPose PoseAt(Vector3 position)
    {
        return AudioListenerPose.Create(position, new Vector3(0f, 0f, -1f), Vector3.UnitY);
    }

    [Fact]
    public void TheStackStartsEmpty_ARegistrationFillsIt_AndASourceIsNeverRegisteredTwice()
    {
        using var service = new AudioService(new FakeAudioBackend());
        var source = new object();

        Assert.False(service.HasListener);

        service.SetListener(source, PoseAt(Vector3.Zero));
        service.SetListener(source, PoseAt(new Vector3(1f, 0f, 0f)));
        Assert.True(service.HasListener);

        // One removal empties it: the second registration updated the pose, it did not add a listener.
        service.RemoveListener(source);
        Assert.False(service.HasListener);
    }

    [Fact]
    public void ANullSource_IsAProgrammingError()
    {
        using var service = new AudioService(new FakeAudioBackend());

        Assert.Throws<ArgumentNullException>(() => service.SetListener(null, PoseAt(Vector3.Zero)));
        Assert.Throws<ArgumentNullException>(() => service.RemoveListener(null));
    }

    [Fact]
    public void RemovingAnUnknownSource_IsIgnored()
    {
        using var service = new AudioService(new FakeAudioBackend());
        var known = new object();
        service.SetListener(known, PoseAt(Vector3.Zero));

        service.RemoveListener(new object());

        Assert.True(service.HasListener);
    }

    [Fact]
    public void Dispose_ClearsTheStack_AndALaterRegistrationIsIgnored()
    {
        var service = new AudioService(new FakeAudioBackend());
        var source = new object();
        service.SetListener(source, PoseAt(Vector3.Zero));

        service.Dispose();

        Assert.False(service.HasListener);

        service.SetListener(source, PoseAt(Vector3.Zero));
        Assert.False(service.HasListener);
    }

    [Fact]
    public void ASecondListenerLogsOneThrottledWarning_AndNothingMoreForTheThirdOrAnUpdate()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);

        try
        {
            using var service = new AudioService(new FakeAudioBackend());
            var first = new object();

            service.SetListener(first, PoseAt(Vector3.Zero));
            service.SetListener(first, PoseAt(new Vector3(1f, 0f, 0f)));
            Assert.DoesNotContain(logger.Warnings, IsListenerWarning);

            service.SetListener(new object(), PoseAt(Vector3.Zero));
            service.SetListener(new object(), PoseAt(Vector3.Zero));
            service.SetListener(first, PoseAt(Vector3.Zero));

            Assert.Single(logger.Warnings, IsListenerWarning);
        }
        finally
        {
            Logs.Close();
        }
    }

    private static bool IsListenerWarning(string message) => message.Contains("listener", StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void TheLastRegisteredListenerIsActive_AndARemovalAtTheTopOrInTheMiddleReactivatesTheRightOne(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        var voice = rig.PlayAt(rig.CreateAsset(), new Vector3(0f, 0f, -4f));
        var a = new object();
        var b = new object();
        var c = new object();

        // Distances to the source: 4 from a, 3 from b, 1 from c.
        rig.Service.SetListener(a, PoseAt(Vector3.Zero));
        rig.Tick(2);
        rig.Verify(voice, 0.25f, 0f);

        rig.Service.SetListener(b, PoseAt(new Vector3(0f, 0f, -1f)));
        rig.Tick(2);
        rig.Verify(voice, 1f / 3f, 0f);

        rig.Service.SetListener(c, PoseAt(new Vector3(0f, 0f, -3f)));
        rig.Tick(2);
        rig.Verify(voice, 1f, 0f);

        // Removal in the middle: c stays active.
        rig.Service.RemoveListener(b);
        rig.Tick(2);
        rig.Verify(voice, 1f, 0f);

        // Removal at the top: a, now the previous one, is active again.
        rig.Service.RemoveListener(c);
        rig.Tick(2);
        rig.Verify(voice, 0.25f, 0f);

        // The last one goes: neutral on the next frame.
        rig.Service.RemoveListener(a);
        Assert.False(rig.Service.HasListener);
        rig.Tick(2);
        rig.Verify(voice, 1f, float.NaN);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void AListenerRegisteredAgainWithANewPose_MovesTheSoundNextFrame_WithoutAddingAListener(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        var voice = rig.PlayAt(rig.CreateAsset(), new Vector3(0f, 0f, -4f));

        rig.Service.SetListener(rig.ListenerSource, PoseAt(Vector3.Zero));
        rig.Tick(2);
        rig.Verify(voice, 0.25f, 0f);

        rig.Service.SetListener(rig.ListenerSource, PoseAt(new Vector3(0f, 0f, -3f)));
        rig.Tick(2);
        rig.Verify(voice, 1f, 0f);

        rig.Service.RemoveListener(rig.ListenerSource);
        Assert.False(rig.Service.HasListener);
    }

    [Theory]
    [InlineData(SpatialBackendKind.Software)]
    [InlineData(SpatialBackendKind.Fallback)]
    [InlineData(SpatialBackendKind.Capability)]
    public void ATurnedListener_ChangesThePan(SpatialBackendKind kind)
    {
        using var rig = new SpatialRig(kind);
        var voice = rig.PlayAt(rig.CreateAsset(), new Vector3(0f, 0f, -3f));

        // Looking along +X with Y up, the right vector is +Z: a source at -Z is on the left.
        rig.Service.SetListener(rig.ListenerSource, AudioListenerPose.Create(Vector3.Zero, new Vector3(1f, 0f, 0f), Vector3.UnitY));
        rig.Tick(2);

        rig.Verify(voice, 1f / 3f, -1f);
    }
}
