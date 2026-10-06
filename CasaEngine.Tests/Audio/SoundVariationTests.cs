using CasaEngine.Framework.Audio;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class SoundVariationTests
{
    /// <summary>Scripted <see cref="Random"/> that records the order of the calls it receives.</summary>
    internal sealed class ScriptedRandom : Random
    {
        private readonly Queue<int> _indices;
        private readonly Queue<float> _floats;

        public ScriptedRandom(IEnumerable<int> indices = null, IEnumerable<float> floats = null)
        {
            _indices = new Queue<int>(indices ?? Array.Empty<int>());
            _floats = new Queue<float>(floats ?? Array.Empty<float>());
        }

        public List<string> Calls { get; } = new();

        public override int Next(int maxValue)
        {
            Calls.Add("Next");
            return _indices.Dequeue();
        }

        public override float NextSingle()
        {
            Calls.Add("NextSingle");
            return _floats.Dequeue();
        }
    }

    private static readonly Guid Main = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Second = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid Third = Guid.Parse("00000000-0000-0000-0000-00000000000c");

    private static SoundAsset ThreeFiles()
    {
        var asset = new SoundAsset { AudioFileAssetId = Main };
        asset.VariationAudioFileAssetIds.Add(Second);
        asset.VariationAudioFileAssetIds.Add(Third);
        return asset;
    }

    [Fact]
    public void NoCandidate_ReturnsEmpty_WithoutCallingTheRandom()
    {
        var random = new ScriptedRandom();

        var draw = SoundVariation.Draw(new SoundAsset(), random);

        Assert.Equal(Guid.Empty, draw.AudioFileAssetId);
        Assert.Empty(random.Calls);
    }

    [Fact]
    public void OneCandidate_IsReturned_WithoutCallingTheRandom()
    {
        var random = new ScriptedRandom();

        var draw = SoundVariation.Draw(new SoundAsset { AudioFileAssetId = Main }, random);

        Assert.Equal(Main, draw.AudioFileAssetId);
        Assert.Empty(random.Calls);
    }

    [Fact]
    public void OneCandidateInTheList_WhenTheMainFileIsEmpty_IsReturned_WithoutCall()
    {
        var asset = new SoundAsset();
        asset.VariationAudioFileAssetIds.Add(Second);
        var random = new ScriptedRandom();

        Assert.Equal(Second, SoundVariation.Draw(asset, random).AudioFileAssetId);
        Assert.Empty(random.Calls);
    }

    [Theory]
    [InlineData(0, "a")]
    [InlineData(1, "b")]
    [InlineData(2, "c")]
    public void ThreeFiles_ScriptedIndexPicksTheMatchingFile(int index, string expected)
    {
        var expectedId = expected switch { "a" => Main, "b" => Second, _ => Third };

        var draw = SoundVariation.Draw(ThreeFiles(), new ScriptedRandom(indices: new[] { index }));

        Assert.Equal(expectedId, draw.AudioFileAssetId);
    }

    [Fact]
    public void EmptyEntries_AreSkipped()
    {
        var asset = new SoundAsset { AudioFileAssetId = Guid.Empty };
        asset.VariationAudioFileAssetIds.Add(Guid.Empty);
        asset.VariationAudioFileAssetIds.Add(Second);
        asset.VariationAudioFileAssetIds.Add(Guid.Empty);
        asset.VariationAudioFileAssetIds.Add(Third);

        var random = new ScriptedRandom(indices: new[] { 1 });

        Assert.Equal(Third, SoundVariation.Draw(asset, random).AudioFileAssetId);
        Assert.Single(random.Calls);
    }

    [Fact]
    public void VolumeRange_MapsTheUnitDrawLinearly()
    {
        var asset = new SoundAsset { VariationVolumeMin = 0.5f, VariationVolumeMax = 1f };

        var low = SoundVariation.Draw(asset, new ScriptedRandom(floats: new[] { 0f }));
        var mid = SoundVariation.Draw(asset, new ScriptedRandom(floats: new[] { 0.5f }));

        Assert.Equal(0.5f, low.VolumeFactor, 5);
        Assert.Equal(0.75f, mid.VolumeFactor, 5);
    }

    [Fact]
    public void InvertedRange_IsSortedAtDrawTime()
    {
        var asset = new SoundAsset { VariationVolumeMin = 1f, VariationVolumeMax = 0.5f };

        var draw = SoundVariation.Draw(asset, new ScriptedRandom(floats: new[] { 0.5f }));

        Assert.Equal(0.75f, draw.VolumeFactor, 5);
    }

    [Fact]
    public void DegenerateRange_DrawsNothing()
    {
        var asset = new SoundAsset
        {
            VariationVolumeMin = 0.5f,
            VariationVolumeMax = 0.5f,
            VariationPitchMin = 0.25f,
            VariationPitchMax = 0.25f,
        };
        var random = new ScriptedRandom();

        var draw = SoundVariation.Draw(asset, random);

        Assert.Equal(0.5f, draw.VolumeFactor);
        Assert.Equal(0.25f, draw.PitchOffset);
        Assert.Empty(random.Calls);
    }

    [Fact]
    public void CallOrder_IsFileThenVolumeThenPitch()
    {
        var asset = ThreeFiles();
        asset.VariationVolumeMin = 0.5f;
        asset.VariationPitchMin = -0.5f;
        asset.VariationPitchMax = 0.5f;
        var random = new ScriptedRandom(indices: new[] { 1 }, floats: new[] { 0f, 1f });

        SoundVariation.Draw(asset, random);

        Assert.Equal(new[] { "Next", "NextSingle", "NextSingle" }, random.Calls);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void HostileIndex_IsClamped(int hostile)
    {
        var draw = SoundVariation.Draw(ThreeFiles(), new ScriptedRandom(indices: new[] { hostile }));

        Assert.Equal(hostile < 0 ? Main : Third, draw.AudioFileAssetId);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(float.NaN)]
    [InlineData(-2f)]
    [InlineData(5f)]
    [InlineData(float.PositiveInfinity)]
    public void HostileUnitDraw_IsClamped_WithoutException(float hostile)
    {
        var asset = new SoundAsset { VariationVolumeMin = 0.5f, VariationVolumeMax = 1f };

        var draw = SoundVariation.Draw(asset, new ScriptedRandom(floats: new[] { hostile }));

        Assert.InRange(draw.VolumeFactor, 0.5f, 1f);
        Assert.False(float.IsNaN(draw.VolumeFactor));
    }

    [Fact]
    public void NeutralDraw_ApplyTo_ReturnsTheInputUnchanged()
    {
        var parameters = new AudioVoiceParameters(0.6f, 0.2f, 0.1f, true)
            .WithLoopRegion(10, 200)
            .WithRateMultiplier(2f);

        var draw = SoundVariation.Draw(new SoundAsset(), new ScriptedRandom());
        var result = draw.ApplyTo(parameters);

        Assert.Equal(parameters, result);
        Assert.True(result.HasLoopRegion);
        Assert.Equal(2f, result.RateMultiplier);
    }

    [Fact]
    public void NonNeutralDraw_KeepsTheLoopRegionAndTheRateMultiplier()
    {
        var parameters = new AudioVoiceParameters(0.8f, 0f, 0f, true)
            .WithLoopRegion(10, 200)
            .WithRateMultiplier(2f);

        var result = new SoundVariationDraw(Main, 0.5f, 0.25f).ApplyTo(parameters);

        Assert.Equal(0.4f, result.Volume, 5);
        Assert.Equal(0.25f, result.Pitch, 5);
        Assert.True(result.HasLoopRegion);
        Assert.Equal(10, result.LoopStartFrame);
        Assert.Equal(200, result.LoopEndFrame);
        Assert.Equal(2f, result.RateMultiplier);
    }

    [Fact]
    public void ApplyTo_ClampsVolumeAndPitch()
    {
        var result = new SoundVariationDraw(Main, 1f, 0.5f)
            .ApplyTo(new AudioVoiceParameters(0.5f, 0f, 0.8f, false));

        Assert.True(result.Volume <= 1f);
        Assert.Equal(0.5f, result.Volume, 5);
        Assert.Equal(1f, result.Pitch, 5);
    }
}
