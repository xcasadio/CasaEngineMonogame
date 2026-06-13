using System;

namespace CasaEngine.Editor.Controls.Timeline;

public sealed class TimelineViewTransform
{
    public float PixelsPerSecond { get; set; } = 96f;

    public float ScrollX { get; set; }

    public float TimeToContentX(float timeSeconds)
    {
        return Math.Max(0f, timeSeconds) * Math.Max(PixelsPerSecond, 0f);
    }

    public float ContentXToTime(float contentX)
    {
        float actualPixelsPerSecond = Math.Max(PixelsPerSecond, 0f);
        if (actualPixelsPerSecond <= 0f)
        {
            return 0f;
        }

        return Math.Max(0f, contentX) / actualPixelsPerSecond;
    }

    public float TimeToViewportX(float timeSeconds)
    {
        return TimeToContentX(timeSeconds) - ScrollX;
    }

    public float ViewportXToTime(float viewportX)
    {
        return ContentXToTime(viewportX + ScrollX);
    }

    public float GetContentWidth(float durationSeconds)
    {
        return TimeToContentX(durationSeconds);
    }

    public float GetScrollXForAnchor(float anchorTimeSeconds, float anchorViewportX, float pixelsPerSecond)
    {
        float actualAnchorTimeSeconds = Math.Max(0f, anchorTimeSeconds);
        float actualAnchorViewportX = Math.Max(0f, anchorViewportX);
        float actualPixelsPerSecond = Math.Max(0f, pixelsPerSecond);
        return (actualAnchorTimeSeconds * actualPixelsPerSecond) - actualAnchorViewportX;
    }

    public float GetMaxScrollX(float durationSeconds, float viewportWidth)
    {
        return Math.Max(0f, GetContentWidth(durationSeconds) - Math.Max(0f, viewportWidth));
    }

    public float ClampScrollX(float desiredScrollX, float durationSeconds, float viewportWidth)
    {
        return Math.Clamp(desiredScrollX, 0f, GetMaxScrollX(durationSeconds, viewportWidth));
    }
}