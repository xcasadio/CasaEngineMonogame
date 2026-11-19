using System;

namespace CasaEngine.Editor.Controls.Timeline;

internal static class TimelineVerticalScroll
{
    /// <summary>Returns the vertical offset that brings a track fully into view with the least movement.
    /// Track rows start after the top padding of the track area. The first track also reveals that padding,
    /// and the last track the bottom padding is left to the scroll range.</summary>
    public static float GetOffsetToReveal(int trackIndex, float currentOffset, float viewportHeight, int rowHeight = TimelineControlMetrics.TrackRowHeight)
    {
        float trackTop = TimelineControlMetrics.ViewportVerticalPadding + (trackIndex * rowHeight);
        float trackBottom = trackTop + rowHeight;
        if (trackIndex == 0)
        {
            trackTop = 0f;
        }

        if (viewportHeight <= 0f)
        {
            return currentOffset;
        }

        if (trackTop < currentOffset)
        {
            return Math.Max(0f, trackTop);
        }

        if (trackBottom > currentOffset + viewportHeight)
        {
            return Math.Max(0f, trackBottom - viewportHeight);
        }

        return currentOffset;
    }
}
