using System.Reflection;
using CasaEngine.Editor.Controls;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// Covers how the editor writers keep the corner track of <c>.anim2d</c> (E19.g G2b-2, ADR-0069): the asset serializer
/// (<c>EditorAssetJsonSerializer</c>, the writer of the converter) and the undo snapshot of the animation editor panel. Both
/// write <c>corner_keyframes</c> only when the list is not empty, and a disabled key without corners nor offset.
/// </summary>
public class Animation2dCornerTrackSerializationTests
{
    private const string ActiveKeyJson = """
        {
          "time_seconds": 0.125,
          "enabled": true,
          "top_left": { "x": -12.0, "y": 12.0 },
          "top_right": { "x": 12.0, "y": 11.0 },
          "bottom_left": { "x": -10.0, "y": -12.0 },
          "bottom_right": { "x": 13.0, "y": -9.0 },
          "source_offset": { "x": -1, "y": 0 }
        }
        """;

    private const string DisabledKeyJson = """
        { "time_seconds": 0.375, "enabled": false }
        """;

    [Fact]
    public void Serializer_WritesAnActiveKeyWithEveryField()
    {
        var animation = Load(CornerTrackJson(ActiveKeyJson, DisabledKeyJson));

        var track = FindCornerTrack(Serialize(animation));

        Assert.True(JToken.DeepEquals(JObject.Parse(ActiveKeyJson), track["corner_keyframes"]![0]));
    }

    [Fact]
    public void Serializer_WritesADisabledKeyWithoutCornersNorOffset()
    {
        var animation = Load(CornerTrackJson(ActiveKeyJson, DisabledKeyJson));

        var savedKey = (JObject)FindCornerTrack(Serialize(animation))["corner_keyframes"]![1]!;

        Assert.True(JToken.DeepEquals(JObject.Parse(DisabledKeyJson), savedKey));
        Assert.Null(savedKey["top_left"]);
        Assert.Null(savedKey["source_offset"]);
    }

    [Fact]
    public void Serializer_WritesNoCornerKeyframes_WhenTheListIsEmpty()
    {
        var animation = Load(PositionOnlyJson());

        foreach (var track in (JArray)Serialize(animation)["tracks"]!)
        {
            Assert.Null(track["corner_keyframes"]);
        }
    }

    [Fact]
    public void EditorUndoSnapshot_KeepsTheCornerKeys()
    {
        var animation = Load(CornerTrackJson(ActiveKeyJson, DisabledKeyJson));

        var track = FindCornerTrack(Snapshot(animation));

        Assert.True(JToken.DeepEquals(JObject.Parse(ActiveKeyJson), track["corner_keyframes"]![0]));
        Assert.True(JToken.DeepEquals(JObject.Parse(DisabledKeyJson), track["corner_keyframes"]![1]));
    }

    [Fact]
    public void EditorUndoSnapshot_WritesNoCornerKeyframes_WhenTheListIsEmpty()
    {
        var animation = Load(PositionOnlyJson());

        foreach (var track in (JArray)Snapshot(animation)["tracks"]!)
        {
            Assert.Null(track["corner_keyframes"]);
        }
    }

    private static JObject Snapshot(Animation2dData animation)
    {
        var method = typeof(Animation2dAssetInspectorPanel).GetMethod("SerializeAnimationData", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (JObject)method!.Invoke(null, new object[] { animation })!;
    }

    private static JObject Serialize(Animation2dData animation)
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(animation, out var root));
        return root;
    }

    private static JObject FindCornerTrack(JObject saved)
    {
        return (JObject)((JArray)saved["tracks"]!).Single(t => (string?)t["property"] == "Corners");
    }

    private static Animation2dData Load(string json)
    {
        var animation = new Animation2dData();
        animation.Load(JObject.Parse(json));
        return animation;
    }

    private static string CornerTrackJson(params string[] cornerKeys)
    {
        return $$"""
            {
              "id": "99999999-0000-0000-0000-000000000011",
              "name": "corner_test",
              "animation_type": "Once",
              "event_track_name": "Track 3",
              "parts": [
                { "id": "p0", "name": "p0", "default_sprite_id": "00000000-0000-0000-0000-000000000000", "default_position": { "x": 0.0, "y": 0.0 }, "default_rotation": 0.0, "default_draw_order": 0, "default_visible": true, "default_flip_x": false, "default_flip_y": false }
              ],
              "tracks": [
                { "name": "Track 1", "target_part_id": "p0", "property": "Position", "interpolation": "Step",
                  "position_keyframes": [ { "time_seconds": 0.0, "value": { "x": 100.0, "y": 50.0 } } ] },
                { "name": "Track 2", "target_part_id": "p0", "property": "Corners", "interpolation": "Step",
                  "corner_keyframes": [ {{string.Join(",", cornerKeys)}} ] }
              ]
            }
            """;
    }

    private static string PositionOnlyJson()
    {
        return """
            {
              "id": "99999999-0000-0000-0000-000000000012",
              "name": "plain",
              "animation_type": "Once",
              "event_track_name": "Track 2",
              "parts": [
                { "id": "p0", "name": "p0", "default_sprite_id": "00000000-0000-0000-0000-000000000000", "default_position": { "x": 0.0, "y": 0.0 }, "default_rotation": 0.0, "default_draw_order": 0, "default_visible": true, "default_flip_x": false, "default_flip_y": false }
              ],
              "tracks": [
                { "name": "Track 1", "target_part_id": "p0", "property": "Position", "interpolation": "Step",
                  "position_keyframes": [ { "time_seconds": 0.0, "value": { "x": 1.0, "y": 2.0 } } ] }
              ]
            }
            """;
    }
}
