using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Controls.Timeline;

internal static class TimelineHitTest
{
    public static TimelineItem HitTestNearestItem(
        IReadOnlyList<TimelineItem> items,
        TimelineViewTransform transform,
        float contentLeft,
        Guid trackId,
        float itemCenterY,
        float hitRadius,
        Point layoutPosition)
    {
        TimelineItem result = null;
        float bestDistanceSquared = float.MaxValue;
        float hitRadiusSquared = hitRadius * hitRadius;

        for (var index = 0; index < items.Count; index++)
        {
            TimelineItem item = items[index];
            if (item.TrackId != trackId)
            {
                continue;
            }

            float x = contentLeft + transform.TimeToViewportX(item.StartTime);
            float dx = layoutPosition.X - x;
            float dy = layoutPosition.Y - itemCenterY;
            float distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared > hitRadiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            result = item;
        }

        return result;
    }

    public static TimelineHitTestResult HitTestTrack(
        IReadOnlyList<TimelineItem> items,
        TimelineViewTransform transform,
        float contentLeft,
        TimelineTrack track,
        Rectangle trackBounds,
        float instantHitRadius,
        float resizeHandleWidth,
        Point position)
    {
        float time = transform.ViewportXToTime(position.X - contentLeft);
        float centerY = trackBounds.Center.Y;
        float hitRadiusSquared = instantHitRadius * instantHitRadius;

        TimelineItem bestInstant = null;
        float bestDistanceSquared = float.MaxValue;

        for (var index = 0; index < items.Count; index++)
        {
            TimelineItem item = items[index];
            if (item.TrackId != track.Id)
            {
                continue;
            }

            float startX = contentLeft + transform.TimeToViewportX(item.StartTime);

            if (item.Kind == TimelineItemKind.Duration || item.Kind == TimelineItemKind.Range)
            {
                float endX = contentLeft + transform.TimeToViewportX(item.StartTime + Math.Max(0f, item.Duration));
                if (position.Y < trackBounds.Top + 2 || position.Y > trackBounds.Bottom - 2)
                {
                    continue;
                }

                if (position.X < startX - resizeHandleWidth || position.X > endX + resizeHandleWidth)
                {
                    continue;
                }

                TimelineHitTestArea area = TimelineHitTestArea.ItemBody;
                if (item.CanResizeStart && Math.Abs(position.X - startX) <= resizeHandleWidth)
                {
                    area = TimelineHitTestArea.ResizeStart;
                }
                else if (item.CanResizeEnd && Math.Abs(position.X - endX) <= resizeHandleWidth)
                {
                    area = TimelineHitTestArea.ResizeEnd;
                }

                return new TimelineHitTestResult
                {
                    Track = track,
                    Item = item,
                    Area = area,
                    Time = time,
                };
            }

            float dx = position.X - startX;
            float dy = position.Y - centerY;
            float distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared > hitRadiusSquared || distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestInstant = item;
        }

        return new TimelineHitTestResult
        {
            Track = track,
            Item = bestInstant,
            Area = bestInstant != null ? TimelineHitTestArea.ItemBody : TimelineHitTestArea.TrackBody,
            Time = time,
        };
    }
}
