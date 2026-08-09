using CasaEngine.Editor.Runtime;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

public sealed class EditorViewportViewStateSerializerTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsModeAndCameraState()
    {
        var state = new EditorViewportViewState(
            is2dViewMode: true,
            new EditorViewport2dCameraState(new Vector3(128f, -64f, 2f), 3, pixelSnap: false));

        var json = EditorViewportViewStateSerializer.Save(state);

        Assert.True(EditorViewportViewStateSerializer.TryLoad(json, out var restored));
        Assert.True(restored.Is2dViewMode);
        Assert.Equal(state.Camera2dState.Target, restored.Camera2dState.Target);
        Assert.Equal(3, restored.Camera2dState.ZoomStep);
        Assert.False(restored.Camera2dState.PixelSnap);
    }

    [Fact]
    public void SaveAndLoad_SurvivesTextSerialization()
    {
        var state = new EditorViewportViewState(
            is2dViewMode: false,
            new EditorViewport2dCameraState(new Vector3(-10.5f, 7.25f, 0f), -2, pixelSnap: true));

        var json = JObject.Parse(EditorViewportViewStateSerializer.Save(state).ToString());

        Assert.True(EditorViewportViewStateSerializer.TryLoad(json, out var restored));
        Assert.False(restored.Is2dViewMode);
        Assert.Equal(-10.5f, restored.Camera2dState.Target.X, 5);
        Assert.Equal(7.25f, restored.Camera2dState.Target.Y, 5);
        Assert.Equal(-2, restored.Camera2dState.ZoomStep);
        Assert.True(restored.Camera2dState.PixelSnap);
    }

    [Fact]
    public void TryLoad_ReturnsFalseForUnrelatedJson()
    {
        Assert.False(EditorViewportViewStateSerializer.TryLoad(new JObject(), out _));
        Assert.False(EditorViewportViewStateSerializer.TryLoad(null, out _));
    }

    [Fact]
    public void TryLoad_UsesDefaultsForMissingFields()
    {
        var json = new JObject
        {
            ["world_viewport"] = new JObject(),
        };

        Assert.True(EditorViewportViewStateSerializer.TryLoad(json, out var restored));
        Assert.False(restored.Is2dViewMode);
        Assert.Equal(Vector3.Zero, restored.Camera2dState.Target);
        Assert.Equal(0, restored.Camera2dState.ZoomStep);
        Assert.False(restored.Camera2dState.PixelSnap);
    }
}
