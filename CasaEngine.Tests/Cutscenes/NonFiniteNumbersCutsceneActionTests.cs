using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>A number that is not finite (NaN, an infinity, or a literal too large for a float) is refused for every number field of the sprite actions, naming the key.</summary>
public sealed class NonFiniteNumbersCutsceneActionTests
{
    [Theory]
    [InlineData("ShowSprite", "x", "\"entity\":\"A\",\"y\":1")]
    [InlineData("ShowSprite", "y", "\"entity\":\"A\",\"x\":1")]
    [InlineData("MoveSprite", "x", "\"entity\":\"A\",\"y\":1,\"speed\":1")]
    [InlineData("MoveSprite", "y", "\"entity\":\"A\",\"x\":1,\"speed\":1")]
    [InlineData("MoveSprite", "speed", "\"entity\":\"A\",\"x\":1,\"y\":1")]
    [InlineData("TintSprite", "duration", "\"entity\":\"A\",\"color\":[1,2,3]")]
    public void Validate_OfANonFiniteNumber_NamesTheKey(string type, string key, string otherFields)
    {
        foreach (string literal in new[] { "1e39", "-1e39", "NaN", "Infinity", "-Infinity" })
        {
            var node = new JObject
            {
                ["version"] = 1,
                ["root_action"] = JObject.Parse($"{{\"type\":\"{type}\",{otherFields},\"{key}\":{literal}}}"),
            };
            var asset = new CutsceneAsset();
            CutsceneAssetJsonSerializer.Load(asset, node);

            var error = Assert.Single(asset.Validate().Messages);

            Assert.StartsWith($"{type}.{key} must be a finite number", error.Message);
        }
    }

    [Fact]
    public void Validate_OfFiniteNumbers_StaysValid()
    {
        var node = new JObject
        {
            ["version"] = 1,
            ["root_action"] = JObject.Parse("{\"type\":\"MoveSprite\",\"entity\":\"A\",\"x\":-3.5e2,\"y\":1,\"speed\":3.4e38}"),
        };
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, node);

        Assert.True(asset.Validate().IsValid);
    }
}
