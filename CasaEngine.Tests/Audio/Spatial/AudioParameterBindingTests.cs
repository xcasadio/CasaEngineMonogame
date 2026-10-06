using CasaEngine.Framework.Audio.Spatial;
using Xunit;

namespace CasaEngine.Tests.Audio.Spatial;

public class AudioParameterBindingTests
{
    private static AudioParameterBinding Volume(float inMin, float inMax, float outMin, float outMax)
    {
        return new AudioParameterBinding("p", AudioParameterTarget.Volume, inMin, inMax, outMin, outMax);
    }

    private static AudioParameterBinding Pitch(float inMin, float inMax, float outMin, float outMax)
    {
        return new AudioParameterBinding("p", AudioParameterTarget.Pitch, inMin, inMax, outMin, outMax);
    }

    [Fact]
    public void Properties_AreStored()
    {
        var binding = new AudioParameterBinding("speed", AudioParameterTarget.Pitch, 1f, 2f, 3f, 4f);

        Assert.Equal("speed", binding.ParameterName);
        Assert.Equal(AudioParameterTarget.Pitch, binding.Target);
        Assert.Equal(1f, binding.InputMin);
        Assert.Equal(2f, binding.InputMax);
        Assert.Equal(3f, binding.OutputMin);
        Assert.Equal(4f, binding.OutputMax);
    }

    [Fact]
    public void Linear_InputIsClampedToRange()
    {
        var binding = Volume(0f, 10f, 1f, 0.5f);

        // t = 0.5: 1 + (0.5 - 1) * 0.5 = 0.75
        Assert.Equal(0.75f, binding.Evaluate(5f), 5);
        Assert.Equal(1f, binding.Evaluate(-3f), 5);
        Assert.Equal(0.5f, binding.Evaluate(20f), 5);
    }

    [Fact]
    public void InvertedInputRange_IsAccepted()
    {
        // Input 10..0 -> output 0..1: input 7.5 gives t = 0.25, output 0.25.
        var binding = Volume(10f, 0f, 0f, 1f);

        Assert.Equal(0.25f, binding.Evaluate(7.5f), 5);
        Assert.Equal(0f, binding.Evaluate(50f), 5);
        Assert.Equal(1f, binding.Evaluate(-50f), 5);
    }

    [Fact]
    public void InvertedOutputRange_IsAccepted()
    {
        var binding = Volume(0f, 1f, 0.8f, 0.2f);

        Assert.Equal(0.8f, binding.Evaluate(0f), 5);
        Assert.Equal(0.2f, binding.Evaluate(1f), 5);
    }

    [Fact]
    public void DegenerateInputRange_IsAStep()
    {
        var binding = Volume(5f, 5f, 0.2f, 0.9f);

        Assert.Equal(0.2f, binding.Evaluate(4.99f), 5);
        Assert.Equal(0.9f, binding.Evaluate(5f), 5);
        Assert.Equal(0.9f, binding.Evaluate(100f), 5);
    }

    [Fact]
    public void OutputBounds()
    {
        // Volume in [0, 1]
        var volume = Volume(0f, 1f, -2f, 3f);
        Assert.Equal(0f, volume.Evaluate(0f));
        Assert.Equal(1f, volume.Evaluate(1f));
        Assert.Equal(1f, volume.Evaluate(0.9f));

        // Pitch in [-1, 1] octave
        var pitch = Pitch(0f, 1f, -3f, 3f);
        Assert.Equal(-1f, pitch.Evaluate(0f));
        Assert.Equal(1f, pitch.Evaluate(1f));
        Assert.Equal(0f, pitch.Evaluate(0.5f), 5);
        Assert.Equal(-0.5f, Pitch(0f, 1f, -0.5f, 0.5f).Evaluate(0f), 5);
    }

    [Fact]
    public void NanInput_ReturnsClampedOutputMin()
    {
        Assert.Equal(0.6f, Volume(0f, 10f, 0.6f, 0f).Evaluate(float.NaN), 5);
        Assert.Equal(1f, Volume(0f, 10f, 5f, 0f).Evaluate(float.NaN));
        Assert.Equal(-1f, Pitch(0f, 10f, -4f, 0f).Evaluate(float.NaN));
        Assert.Equal(0.6f, Volume(5f, 5f, 0.6f, 1f).Evaluate(float.NaN), 5);
    }

    [Fact]
    public void Evaluate_NeverReturnsNonFinite()
    {
        var nan = float.NaN;
        var inf = float.PositiveInfinity;
        float[] values = { nan, inf, -inf, 0f, 1f, -1f, 5f, 1e30f };

        foreach (var inMin in values)
        {
            foreach (var inMax in values)
            {
                foreach (var outMin in values)
                {
                    foreach (var outMax in values)
                    {
                        foreach (var input in values)
                        {
                            Assert.True(float.IsFinite(Volume(inMin, inMax, outMin, outMax).Evaluate(input)));
                            Assert.True(float.IsFinite(Pitch(inMin, inMax, outMin, outMax).Evaluate(input)));
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void Evaluate_DoesNotAllocate()
    {
        var binding = Volume(0f, 10f, 1f, 0.5f);
        var sum = binding.Evaluate(3f);

        var before = AllocationWindow.Start();
        for (var i = 0; i < 100; i++)
        {
            sum += binding.Evaluate(i);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(sum > 0f);
    }
}
