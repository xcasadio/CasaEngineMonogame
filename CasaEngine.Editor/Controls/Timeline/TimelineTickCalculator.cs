using System;

namespace CasaEngine.Editor.Controls.Timeline;

internal static class TimelineTickCalculator
{
    private static readonly float[] MajorTickCandidatesSeconds =
    {
        0.1f,
        0.25f,
        0.5f,
        1f,
        2f,
        5f,
        10f,
        15f,
        30f,
        60f,
    };

    public static float GetMajorTickStepSeconds(float pixelsPerSecond, float targetPixels)
    {
        float actualPixelsPerSecond = Math.Max(pixelsPerSecond, 0f);
        float actualTargetPixels = Math.Max(targetPixels, 1f);

        for (var index = 0; index < MajorTickCandidatesSeconds.Length; index++)
        {
            float candidate = MajorTickCandidatesSeconds[index];
            if (candidate * actualPixelsPerSecond >= actualTargetPixels)
            {
                return candidate;
            }
        }

        return MajorTickCandidatesSeconds[^1];
    }

    public static float GetMinorTickStepSeconds(float majorTickStepSeconds)
    {
        return Math.Max(majorTickStepSeconds / 5f, 0.02f);
    }
}