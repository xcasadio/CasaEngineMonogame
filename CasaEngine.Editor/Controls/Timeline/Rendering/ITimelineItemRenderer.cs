#nullable enable

using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls.Timeline.Rendering;

public interface ITimelineItemRenderer
{
    void DrawItem(
        ElementDrawArgs drawArgs,
        TimelineRenderContext context,
        TimelineTrack track,
        TimelineItem item,
        RectangleF bounds,
        TimelineItemVisualState state);

    bool HitTest(
        TimelineRenderContext context,
        TimelineTrack track,
        TimelineItem item,
        RectangleF bounds,
        Point mousePosition);
}
