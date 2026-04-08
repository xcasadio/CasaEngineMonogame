using CasaEngine.Framework.Rendering.Environment;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ReflectionProbeSelectorTests
{
    [Fact]
    public void ComputeWeight_ReturnsZeroOutsideInfluenceRadius()
    {
        var probe = new ReflectionProbe
        {
            Position = Vector3.Zero,
            InfluenceRadius = 5.0f,
            BlendDistance = 2.0f,
        };

        float weight = ReflectionProbeSelector.ComputeWeight(new Vector3(6.0f, 0.0f, 0.0f), probe);

        Assert.Equal(0.0f, weight);
    }

    [Fact]
    public void Select_ReturnsClosestProbesSortedAndNormalized()
    {
        var probes = new ReflectionProbeCollection();
        probes.Add(new ReflectionProbe
        {
            Position = Vector3.Zero,
            InfluenceRadius = 10.0f,
            BlendDistance = 4.0f,
            SpecularCubemapAssetId = Guid.Parse("0af56d5a-0f4e-4769-b7d2-bec4437d847b"),
        });
        probes.Add(new ReflectionProbe
        {
            Position = new Vector3(5.0f, 0.0f, 0.0f),
            InfluenceRadius = 10.0f,
            BlendDistance = 4.0f,
            SpecularCubemapAssetId = Guid.Parse("f1d3445f-e49c-45c2-8a07-1e3917ca6db8"),
        });

        Span<ResolvedReflectionProbe> selectedProbes = stackalloc ResolvedReflectionProbe[ReflectionProbeSelector.MaxSelectedProbes];

        int count = ReflectionProbeSelector.Select(new Vector3(7.5f, 0.0f, 0.0f), probes, selectedProbes, out float localInfluence);

        Assert.Equal(2, count);
        Assert.True(localInfluence > 0.0f);
        Assert.True(selectedProbes[0].Weight >= selectedProbes[1].Weight);
        Assert.Equal(1.0f, selectedProbes[0].Weight + selectedProbes[1].Weight, 3);
        Assert.Equal(Guid.Parse("f1d3445f-e49c-45c2-8a07-1e3917ca6db8"), selectedProbes[0].SpecularCubemapAssetId);
    }

    [Fact]
    public void Select_IgnoresDisabledProbes()
    {
        var probes = new ReflectionProbeCollection();
        probes.Add(new ReflectionProbe
        {
            Enabled = false,
            Position = Vector3.Zero,
            InfluenceRadius = 10.0f,
        });

        Span<ResolvedReflectionProbe> selectedProbes = stackalloc ResolvedReflectionProbe[ReflectionProbeSelector.MaxSelectedProbes];

        int count = ReflectionProbeSelector.Select(Vector3.Zero, probes, selectedProbes);

        Assert.Equal(0, count);
    }
}