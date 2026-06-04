using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Controls.Timeline;

internal static class TimelineHitTest
{
    public static TimelineEvent? HitTestNearestEvent(
        IReadOnlyList<TimelineEvent> events,
        TimelineViewTransform transform,
        float contentLeft,
        float eventCenterY,
        float hitRadius,
        Point layoutPosition)
    {
        TimelineEvent? result = null;
        float bestDistanceSquared = float.MaxValue;
        float hitRadiusSquared = hitRadius * hitRadius;

        for (var index = 0; index < events.Count; index++)
        {
            TimelineEvent timelineEvent = events[index];
            float x = contentLeft + transform.TimeToViewportX(timelineEvent.TimeSeconds);
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