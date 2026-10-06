using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio.Spatial;
using Xunit;

namespace CasaEngine.Tests.Audio.Spatial;

/// <summary>Log-capturing tests rely on the process-global <see cref="Logs"/>, hence the serialized collection.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioGameParameterRegistryTests
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

    [Fact]
    public void Indices_AreStableAndCaseInsensitive()
    {
        var registry = new AudioGameParameterRegistry();

        var a = registry.GetOrCreateIndex("Speed");
        var b = registry.GetOrCreateIndex("health");

        Assert.Equal(0, a);
        Assert.Equal(1, b);
        Assert.Equal(a, registry.GetOrCreateIndex("SPEED"));
        Assert.Equal(b, registry.GetOrCreateIndex("Health"));
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void NullOrEmptyName_IsMinusOneWithoutWarning()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            var registry = new AudioGameParameterRegistry();

            Assert.Equal(-1, registry.GetOrCreateIndex(null));
            Assert.Equal(-1, registry.GetOrCreateIndex(string.Empty));
            Assert.Empty(logger.Warnings);
            Assert.Equal(0, registry.Count);
        }
        finally
        {
            Logs.Close();
        }
    }

    [Fact]
    public void FullRegistry_ReturnsMinusOneWithASingleWarning()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            var registry = new AudioGameParameterRegistry();

            for (var i = 0; i < 64; i++)
            {
                Assert.Equal(i, registry.GetOrCreateIndex("p" + i));
            }

            Assert.Empty(logger.Warnings);
            Assert.Equal(-1, registry.GetOrCreateIndex("p64"));
            Assert.Equal(-1, registry.GetOrCreateIndex("p65"));
            Assert.Single(logger.Warnings);

            // Existing names still resolve.
            Assert.Equal(10, registry.GetOrCreateIndex("P10"));
        }
        finally
        {
            Logs.Close();
        }
    }

    [Fact]
    public void Get_NeverWrittenOrInvalidIndex_IsNaN()
    {
        var registry = new AudioGameParameterRegistry();
        var index = registry.GetOrCreateIndex("a");

        Assert.True(float.IsNaN(registry.Get(index)));
        Assert.True(float.IsNaN(registry.Get(-1)));
        Assert.True(float.IsNaN(registry.Get(5)));
        Assert.True(float.IsNaN(registry.Get(64)));
    }

    [Fact]
    public void SetAndGet_RoundTrip_AndInvalidIndexIsIgnored()
    {
        var registry = new AudioGameParameterRegistry();
        var index = registry.GetOrCreateIndex("a");

        registry.Set(index, 0.75f);
        registry.Set(-1, 1f);
        registry.Set(3, 1f);
        registry.Set(100, 1f);

        Assert.Equal(0.75f, registry.Get(index));
        Assert.Equal(0, registry.GetVersion(-1));
    }

    [Fact]
    public void Version_ChangesOnlyWhenTheValueChanges()
    {
        var registry = new AudioGameParameterRegistry();
        var index = registry.GetOrCreateIndex("a");

        Assert.Equal(0, registry.GetVersion(index));

        registry.Set(index, float.NaN);
        Assert.Equal(0, registry.GetVersion(index));

        registry.Set(index, 1f);
        Assert.Equal(1, registry.GetVersion(index));

        registry.Set(index, 1f);
        Assert.Equal(1, registry.GetVersion(index));

        registry.Set(index, 2f);
        Assert.Equal(2, registry.GetVersion(index));

        registry.Set(index, float.NaN);
        Assert.Equal(3, registry.GetVersion(index));

        registry.Set(index, float.NaN);
        Assert.Equal(3, registry.GetVersion(index));
    }

    [Fact]
    public void ExistingNameSetAndGet_DoNotAllocate()
    {
        var registry = new AudioGameParameterRegistry();
        registry.GetOrCreateIndex("alpha");
        var index = registry.GetOrCreateIndex("Speed");
        registry.Set(index, 0.5f);
        var sum = 0f;

        var before = AllocationWindow.Start();
        for (var i = 0; i < 100; i++)
        {
            var found = registry.GetOrCreateIndex("SPEED");
            registry.Set(found, i);
            sum += registry.Get(found);
            sum += registry.GetVersion(found);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(sum > 0f);
    }
}
