using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Selects the highest-weight local reflection probes for a given world-space position.
/// </summary>
public static class ReflectionProbeSelector
{
    public const int MaxSelectedProbes = 2;

    public static int Select(Vector3 worldPosition, IReadOnlyList<ReflectionProbe> probes, Span<ResolvedReflectionProbe> selectedProbes)
        => Select(worldPosition, probes, selectedProbes, out _);

    public static int Select(Vector3 worldPosition, IReadOnlyList<ReflectionProbe> probes, Span<ResolvedReflectionProbe> selectedProbes, out float localInfluence)
    {
        ArgumentNullException.ThrowIfNull(probes);

        localInfluence = 0.0f;

        if (selectedProbes.Length == 0 || probes.Count == 0)
        {
            return 0;
        }

        int selectedCount = 0;
        float totalRawWeight = 0.0f;
        for (int probeIndex = 0; probeIndex < probes.Count; probeIndex++)
        {
            ReflectionProbe probe = probes[probeIndex];
            float weight = ComputeWeight(worldPosition, probe);
            if (weight <= 0.0f)
            {
                continue;
            }

            totalRawWeight += weight;

            var resolvedProbe = new ResolvedReflectionProbe
            {
                ProbeId = probe.Id,
                EnvironmentAssetId = probe.EnvironmentAssetId,
                SpecularCubemapAssetId = probe.SpecularCubemapAssetId,
                Position = probe.Position,
                InfluenceRadius = probe.InfluenceRadius,
                Weight = weight,
            };

            InsertSorted(selectedProbes, ref selectedCount, resolvedProbe);
        }

        NormalizeWeights(selectedProbes, selectedCount);
        localInfluence = Math.Clamp(totalRawWeight, 0.0f, 1.0f);
        return selectedCount;
    }

    internal static float ComputeWeight(Vector3 worldPosition, ReflectionProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        if (!probe.Enabled || probe.InfluenceRadius <= 0.0f)
        {
            return 0.0f;
        }

        float distance = Vector3.Distance(worldPosition, probe.Position);
        if (distance >= probe.InfluenceRadius)
        {
            return 0.0f;
        }

        float blendDistance = Math.Clamp(probe.BlendDistance, 0.0f, probe.InfluenceRadius);
        if (blendDistance <= 0.0f)
        {
            return 1.0f;
        }

        float fullWeightRadius = Math.Max(probe.InfluenceRadius - blendDistance, 0.0f);
        if (distance <= fullWeightRadius)
        {
            return 1.0f;
        }

        float fade = (distance - fullWeightRadius) / blendDistance;
        return 1.0f - Math.Clamp(fade, 0.0f, 1.0f);
    }

    private static void InsertSorted(Span<ResolvedReflectionProbe> selectedProbes, ref int selectedCount, in ResolvedReflectionProbe candidate)
    {
        int capacity = Math.Min(selectedProbes.Length, MaxSelectedProbes);
        if (capacity == 0)
        {
            return;
        }

        int insertIndex = 0;
        while (insertIndex < selectedCount && selectedProbes[insertIndex].Weight >= candidate.Weight)
        {
            insertIndex++;
        }

        if (insertIndex >= capacity)
        {
            return;
        }

        if (selectedCount < capacity)
        {
            selectedCount++;
        }

        for (int moveIndex = selectedCount - 1; moveIndex > insertIndex; moveIndex--)
        {
            selectedProbes[moveIndex] = selectedProbes[moveIndex - 1];
        }

        selectedProbes[insertIndex] = candidate;
    }

    private static void NormalizeWeights(Span<ResolvedReflectionProbe> selectedProbes, int selectedCount)
    {
        if (selectedCount == 0)
        {
            return;
        }

        float totalWeight = 0.0f;
        for (int index = 0; index < selectedCount; index++)
        {
            totalWeight += selectedProbes[index].Weight;
        }

        if (totalWeight <= 0.0f)
        {
            return;
        }

        for (int index = 0; index < selectedCount; index++)
        {
            ResolvedReflectionProbe probe = selectedProbes[index];
            selectedProbes[index] = new ResolvedReflectionProbe
            {
                ProbeId = probe.ProbeId,
                EnvironmentAssetId = probe.EnvironmentAssetId,
                SpecularCubemapAssetId = probe.SpecularCubemapAssetId,
                Position = probe.Position,
                InfluenceRadius = probe.InfluenceRadius,
                Weight = probe.Weight / totalWeight,
            };
        }
    }
}