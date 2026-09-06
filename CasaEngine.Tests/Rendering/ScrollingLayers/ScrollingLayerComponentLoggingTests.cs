using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application.Components;
using Xunit;

namespace CasaEngine.Tests.Rendering.ScrollingLayers;

/// <summary>
/// Pins <see cref="ScrollingLayerComponent.ResolveLayerFrames"/>'s warning on both D-E9-9 failure
/// branches (S0 fix item 1: the ported <c>BackdropRenderer.LoadLayerFrames</c> logged on both, this
/// port originally logged on neither). Uses the engine's own log seam - <see cref="Logs.AddLogger"/>
/// to attach a capturing <see cref="ILogger"/>, <see cref="Logs.Close"/> to detach it again in
/// <c>finally</c> - since <see cref="Logs"/> is process-global state with no dedicated remove method;
/// <see cref="ProjectEnvironmentCollection"/> (<c>DisableParallelization = true</c>) keeps this class
/// from running concurrently with anything else that might touch it.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class ScrollingLayerComponentLoggingTests
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

    private static List<string> CaptureWarnings(Action action)
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            action();
        }
        finally
        {
            Logs.Close(); // detaches every logger added during this test, including `logger`.
        }

        return logger.Warnings;
    }

    [Fact]
    public void ResolveLayerFrames_FrameZeroFails_WarnsAndIdentifiesTheLayerAndFrame()
    {
        var frameId = Guid.NewGuid();

        var warnings = CaptureWarnings(() =>
            ScrollingLayerComponent.ResolveLayerFrames(new[] { frameId }, _ => null, layerIndex: 3, stableId: 42));

        var warning = Assert.Single(warnings);
        Assert.Contains("3", warning);
        Assert.Contains("42", warning);
        Assert.Contains("0", warning); // frame index.
        Assert.Contains(frameId.ToString(), warning);
        Assert.Contains("skipped", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveLayerFrames_FrameOneFails_WarnsAndIdentifiesTheLayerAndFrame()
    {
        var frame0Id = Guid.NewGuid();
        var frame1Id = Guid.NewGuid();
        var frame0Texture = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(Microsoft.Xna.Framework.Graphics.Texture2D)) as Microsoft.Xna.Framework.Graphics.Texture2D;

        var warnings = CaptureWarnings(() =>
            ScrollingLayerComponent.ResolveLayerFrames(
                new[] { frame0Id, frame1Id },
                id => id == frame0Id ? frame0Texture : null,
                layerIndex: 7,
                stableId: 99));

        var warning = Assert.Single(warnings);
        Assert.Contains("7", warning);
        Assert.Contains("99", warning);
        Assert.Contains("1", warning); // frame index.
        Assert.Contains(frame1Id.ToString(), warning);
        Assert.Contains("frame 0 only", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveLayerFrames_AllFramesSucceed_NeverWarns()
    {
        var frameId = Guid.NewGuid();
        var frameTexture = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(Microsoft.Xna.Framework.Graphics.Texture2D)) as Microsoft.Xna.Framework.Graphics.Texture2D;

        var warnings = CaptureWarnings(() =>
            ScrollingLayerComponent.ResolveLayerFrames(new[] { frameId }, _ => frameTexture, layerIndex: 0, stableId: 0));

        Assert.Empty(warnings);
    }
}
