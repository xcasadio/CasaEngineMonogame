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

    /// <summary>Picks the most specific item under the cursor: an instant or marker in range wins over any bar,
    /// and among bars and ranges the shortest wins (a duration over a range of equal length, then the later item).</summary>
    public static TimelineHitTestResult HitTestTrackPrecise(
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
        TimelineItem bestBar = null;
        float bestBarStartX = 0f;
        float bestBarEndX = 0f;
        bool barRowHit = position.Y >= trackBounds.Top + 2 && position.Y <= trackBounds.Bottom - 2;

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
                if (!barRowHit)
                {
                    continue;
                }

                float endX = contentLeft + transform.TimeToViewportX(item.StartTime + Math.Max(0f, item.Duration));
                if (position.X < startX - resizeHandleWidth || position.X > endX + resizeHandleWidth)
                {
                    continue;
                }

                if (bestBar != null && IsMoreSpecific(bestBar, item))
                {
                    continue;
                }

                bestBar = item;
                bestBarStartX = startX;
                bestBarEndX = endX;
                continue;
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

        if (bestInstant != null)
        {
            return new TimelineHitTestResult
            {
                Track = track,
                Item = bestInstant,
                Area = TimelineHitTestArea.ItemBody,
                Time = time,
            };
        }

        if (bestBar != null)
        {
            TimelineHitTestArea area = TimelineHitTestArea.ItemBody;
            if (bestBar.CanResizeStart && Math.Abs(position.X - bestBarStartX) <= resizeHandleWidth)
            {
                area = TimelineHitTestArea.ResizeStart;
            }
            else if (bestBar.CanResizeEnd && Math.Abs(position.X - bestBarEndX) <= resizeHandleWidth)
            {
                area = TimelineHitTestArea.ResizeEnd;
            }

            return new TimelineHitTestResult
            {
                Track = track,
                Item = bestBar,
                Area = area,
                Time = time,
            };
        }

        return new TimelineHitTestResult
        {
            Track = track,
            Area = TimelineHitTestArea.TrackBody,
            Time = time,
        };
    }

    /// <summary>True when <paramref name="current"/> stays the pick against the later <paramref name="candidate"/>.</summary>
    private static bool IsMoreSpecific(TimelineItem current, TimelineItem candidate)
    {
        float currentDuration = Math.Max(0f, current.Duration);
        float candidateDuration = Math.Max(0f, candidate.Duration);
        if (currentDuration != candidateDuration)
        {
            return currentDuration < candidateDuration;
        }

        return current.Kind == TimelineItemKind.Duration && candidate.Kind == TimelineItemKind.Range;
    }
}
