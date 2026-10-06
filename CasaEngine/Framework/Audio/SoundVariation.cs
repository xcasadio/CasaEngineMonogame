namespace CasaEngine.Framework.Audio;

/// <summary>
/// What one play of a <see cref="SoundAsset"/> draws from its variations: the audio file, a volume factor
/// and a pitch offset in octaves. A draw without variation is neutral (factor 1, offset 0).
/// </summary>
internal readonly struct SoundVariationDraw
{
    public SoundVariationDraw(Guid audioFileAssetId, float volumeFactor, float pitchOffset)
    {
        AudioFileAssetId = audioFileAssetId;
        VolumeFactor = volumeFactor;
        PitchOffset = pitchOffset;
    }

    public Guid AudioFileAssetId { get; }

    public float VolumeFactor { get; }

    public float PitchOffset { get; }

    /// <summary>
    /// Applies the draw on top of already overridden parameters. A neutral draw returns the input untouched;
    /// otherwise the result is clamped by <see cref="AudioVoiceParameters"/>.
    /// </summary>
    public AudioVoiceParameters ApplyTo(in AudioVoiceParameters parameters)
    {
        if (VolumeFactor == 1f && PitchOffset == 0f)
        {
            return parameters;
        }

        return parameters
            .WithVolume(parameters.Volume * VolumeFactor)
            .WithPitch(parameters.Pitch + PitchOffset);
    }
}

internal static class SoundVariation
{
    /// <summary>
    /// Draws the variations of one play. The random source is called in a fixed order: file
    /// (<c>Next(n)</c>, only with two candidates or more), volume then pitch (<c>NextSingle()</c>, only for a
    /// non-degenerate range). Values it returns are clamped, so a faulty <see cref="Random"/> never throws.
    /// Does not allocate.
    /// </summary>
    public static SoundVariationDraw Draw(SoundAsset asset, Random random)
    {
        var fileId = DrawFile(asset, random);
        var volumeFactor = DrawRange(asset.VariationVolumeMin, asset.VariationVolumeMax, random);
        var pitchOffset = DrawRange(asset.VariationPitchMin, asset.VariationPitchMax, random);

        return new SoundVariationDraw(fileId, volumeFactor, pitchOffset);
    }

    private static Guid DrawFile(SoundAsset asset, Random random)
    {
        var variations = asset.VariationAudioFileAssetIds;
        var main = asset.AudioFileAssetId;

        var count = main != Guid.Empty ? 1 : 0;
        for (var i = 0; i < variations.Count; i++)
        {
            if (variations[i] != Guid.Empty)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return Guid.Empty;
        }

        var index = 0;
        if (count > 1)
        {
            index = Math.Clamp(random.Next(count), 0, count - 1);
        }

        if (main != Guid.Empty)
        {
            if (index == 0)
            {
                return main;
            }

            index--;
        }

        for (var i = 0; i < variations.Count; i++)
        {
            var id = variations[i];
            if (id == Guid.Empty)
            {
                continue;
            }

            if (index == 0)
            {
                return id;
            }

            index--;
        }

        return Guid.Empty;
    }

    private static float DrawRange(float first, float second, Random random)
    {
        var lo = Math.Min(first, second);
        var hi = Math.Max(first, second);

        if (!(hi > lo))
        {
            return lo;
        }

        var u = random.NextSingle();
        if (float.IsNaN(u) || u < 0f)
        {
            u = 0f;
        }
        else if (u >= 1f)
        {
            u = MathF.BitDecrement(1f);
        }

        return lo + (hi - lo) * u;
    }
}
