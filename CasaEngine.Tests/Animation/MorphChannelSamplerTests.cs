using CasaEngine.Framework.Animations;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class MorphChannelSamplerTests
{
    [Fact]
    public void MorphChannelSampler_SamplesInterpolatedAttachmentWeights()
    {
        var channel = new MorphChannel(
            "Face",
            0,
            new[]
            {
                new MorphKeyframe(0f, new[] { 0, 1 }, new[] { 0f, 1f }),
                new MorphKeyframe(1f, new[] { 0, 1 }, new[] { 1f, 0f }),
            });
        var destinationWeights = new[] { 0.25f, 0.25f };
        var sampler = new MorphChannelSampler();

        sampler.Sample(channel, 0.5f, 1f, loop: false, destinationWeights);

        Assert.Equal(0.5f, destinationWeights[0]);
        Assert.Equal(0.5f, destinationWeights[1]);
    }

    [Fact]
    public void MorphChannelSampler_WrapsBetweenLastAndFirstKeyframes_WhenLooping()
    {
        var channel = new MorphChannel(
            "Face",
            0,
            new[]
            {
                new MorphKeyframe(0.25f, new[] { 0 }, new[] { 0f }),
                new MorphKeyframe(0.75f, new[] { 0 }, new[] { 1f }),
            });
        var destinationWeights = new[] { -1f };
        var sampler = new MorphChannelSampler();

        sampler.Sample(channel, 0f, 1f, loop: true, destinationWeights);

        Assert.Equal(0.5f, destinationWeights[0], 3);
    }
}