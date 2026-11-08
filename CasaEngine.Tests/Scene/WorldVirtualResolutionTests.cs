using CasaEngine.EditorServices;
using CasaEngine.EditorServices.PlayMode;
using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Scene;

/// <summary>
/// The optional <c>virtual_resolution</c> object of a <c>.world</c> (ADR-0087): written only when the world declares it,
/// read back with the same values, absent after a reload of a document without it, refused at load when invalid with
/// the world and the key named, and kept by the play-mode snapshot.
/// </summary>
public class WorldVirtualResolutionTests
{
    private static JObject SavedWorld(World world)
    {
        var node = new JObject();
        EditorEntityJsonSerializer.SaveWorld(world, node);
        return node;
    }

    [Theory]
    [InlineData(VirtualResolutionMode.IntegerFit)]
    [InlineData(VirtualResolutionMode.BufferedIntegerFit)]
    [InlineData(VirtualResolutionMode.BufferedFit)]
    public void SaveThenLoad_RoundTripsTheBlock(VirtualResolutionMode mode)
    {
        var world = new World
        {
            Name = "W",
            VirtualResolution = new VirtualResolutionSettings { Width = 640, Height = 480, Mode = mode },
        };

        var node = SavedWorld(world);

        Assert.Equal(640, (int?)node["virtual_resolution"]?["width"]);
        Assert.Equal(480, (int?)node["virtual_resolution"]?["height"]);
        Assert.Equal(mode.ToString(), (string?)node["virtual_resolution"]?["mode"]);

        var loaded = new World();
        loaded.Load(node);

        Assert.NotNull(loaded.VirtualResolution);
        Assert.Equal(640, loaded.VirtualResolution.Width);
        Assert.Equal(480, loaded.VirtualResolution.Height);
        Assert.Equal(mode, loaded.VirtualResolution.Mode);
        Assert.False(loaded.VirtualResolution.ScaleUI);
    }

    [Fact]
    public void AWorldWithoutTheBlock_WritesNoKey_AndLoadsNull()
    {
        var node = SavedWorld(new World { Name = "W" });

        Assert.False(node.ContainsKey("virtual_resolution"));

        var loaded = new World();
        loaded.Load(node);
        Assert.Null(loaded.VirtualResolution);
    }

    [Fact]
    public void ABlockWithoutTheMode_LoadsIntegerFit()
    {
        var node = SavedWorld(new World { Name = "W" });
        node["virtual_resolution"] = new JObject { ["width"] = 320, ["height"] = 240 };

        var loaded = new World();
        loaded.Load(node);

        Assert.Equal(VirtualResolutionMode.IntegerFit, loaded.VirtualResolution.Mode);
    }

    [Fact]
    public void AReload_WithoutTheBlock_DropsThePreviousOne()
    {
        var withBlock = SavedWorld(new World
        {
            Name = "W",
            VirtualResolution = new VirtualResolutionSettings { Width = 320, Height = 240 },
        });
        var without = SavedWorld(new World { Name = "W" });

        var world = new World();
        world.Load(withBlock);
        Assert.NotNull(world.VirtualResolution);

        world.Load(without);
        Assert.Null(world.VirtualResolution);
    }

    [Theory]
    [InlineData("320", "width")]
    [InlineData("{ \"height\": 240 }", "width")]
    [InlineData("{ \"width\": 320 }", "height")]
    [InlineData("{ \"width\": 0, \"height\": 240 }", "width")]
    [InlineData("{ \"width\": 320, \"height\": -1 }", "height")]
    [InlineData("{ \"width\": \"320\", \"height\": 240 }", "width")]
    [InlineData("{ \"width\": 320.5, \"height\": 240 }", "width")]
    [InlineData("{ \"width\": 320, \"height\": 240, \"mode\": \"Stretch\" }", "mode")]
    [InlineData("{ \"width\": 320, \"height\": 240, \"mode\": 1 }", "mode")]
    public void AnInvalidBlock_FailsNamingTheWorldAndTheKey(string json, string key)
    {
        var node = SavedWorld(new World { Name = "Broken" });
        node["virtual_resolution"] = JToken.Parse(json);

        var error = Assert.Throws<InvalidDataException>(() => new World().Load(node));

        Assert.Contains("Broken", error.Message);
        Assert.Contains("virtual_resolution", error.Message);
        if (json.TrimStart().StartsWith('{'))
        {
            Assert.Contains(key, error.Message);
        }
    }

    [Fact]
    public void ThePlayModeSnapshot_KeepsTheBlock()
    {
        var editWorld = new World
        {
            Name = "W",
            FileName = "W.world",
            VirtualResolution = new VirtualResolutionSettings { Width = 640, Height = 480, Mode = VirtualResolutionMode.BufferedFit },
        };

        var playWorld = EditorWorldPlaySnapshot.CreatePlayWorld(editWorld);

        Assert.NotNull(playWorld.VirtualResolution);
        Assert.Equal(640, playWorld.VirtualResolution.Width);
        Assert.Equal(480, playWorld.VirtualResolution.Height);
        Assert.Equal(VirtualResolutionMode.BufferedFit, playWorld.VirtualResolution.Mode);
    }
}
