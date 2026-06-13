using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Controls.Timeline;

internal static class TimelineHitTest
{
    public static TimelineItem HitTestNearestEvent(
        IReadOnlyList<TimelineItem> events,
        TimelineViewTransform transform,
        float contentLeft,
        Guid laneId,
        float eventCenterY,
        float hitRadius,
        Point layoutPosition)
    {
        TimelineItem result = null;
        float bestDistanceSquared = float.MaxValue;
        float hitRadiusSquared = hitRadius * hitRadius;

        for (var index = 0; index < events.Count; index++)
        {
            TimelineItem timelineEvent = events[index];
            if (timelineEvent.TrackId != laneId)
            {
                continue;
            }

            float x = contentLeft + transform.TimeToViewportX(timelineEvent.StartTime);
            float dx = layoutPosition.X - x;
            float dy = layoutPosition.Y - eventCenterY;
            float distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared > hitRadiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            result = timelineEvent;
        }

        return result;
    }
}