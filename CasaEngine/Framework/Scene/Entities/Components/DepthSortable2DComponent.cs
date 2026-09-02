using System.ComponentModel;
using System.Globalization;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("2D Depth Sortable")]
public sealed class DepthSortable2DComponent : EntityComponent
{
    private const float SortPrecision = 100f;
    private int _resolvedStableId;

    public RenderPass2D RenderPass { get; set; } = RenderPass2D.YSortedWorld;

    public int SortingLayer { get; set; } = (int)RenderPass2D.YSortedWorld;

    public int OrderInLayer { get; set; }

    public int Elevation { get; set; }

    public Vector2 SortAnchorLocal { get; set; }

    public int LocalSortOffset { get; set; }

    public DepthSortMode2D SortMode { get; set; } = DepthSortMode2D.TopDownYUp;

    public int StableId { get; set; }

    public DepthSortable2DComponent()
    {
    }

    /// <summary>Additive constructor for callers assigning a deterministic id (see <see cref="ObjectBase(Guid)"/>).</summary>
    public DepthSortable2DComponent(Guid id) : base(id)
    {
    }

    public DepthSortable2DComponent(DepthSortable2DComponent other) : base(other)
    {
        RenderPass = other.RenderPass;
        SortingLayer = other.SortingLayer;
        OrderInLayer = other.OrderInLayer;
        Elevation = other.Elevation;
        SortAnchorLocal = other.SortAnchorLocal;
        LocalSortOffset = other.LocalSortOffset;
        SortMode = other.SortMode;
        StableId = other.StableId;
        _resolvedStableId = other._resolvedStableId;
    }

    public override EntityComponent Clone()
        => new DepthSortable2DComponent(this);

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        _resolvedStableId = CreateStableId(actor.Id);
    }

    public RenderSortKey2D BuildSortKey(Vector3 worldPosition)
        => BuildSortKey(worldPosition, null);

    public RenderSortKey2D BuildSortKey(Vector3 worldPosition, RenderFrame? frame)
    {
        var sortAnchorWorldPosition = GetSortAnchorWorldPosition(worldPosition);
        var sortCoordinate = ComputeSortCoordinate(sortAnchorWorldPosition, frame);

        return new RenderSortKey2D(
            (int)RenderPass,
            SortingLayer,
            OrderInLayer,
            Elevation,
            sortCoordinate,
            LocalSortOffset,
            GetStableId());
    }

    public Vector2 GetSortAnchorWorldPosition(Vector3 worldPosition)
        => new(worldPosition.X + SortAnchorLocal.X, worldPosition.Y + SortAnchorLocal.Y);

    public override void Load(JObject element)
    {
        base.Load(element);

        RenderPass = ReadEnum(element, "render_pass", RenderPass);
        SortingLayer = ReadInt32(element, "sorting_layer", SortingLayer);
        OrderInLayer = ReadInt32(element, "order_in_layer", OrderInLayer);
        Elevation = ReadInt32(element, "elevation", Elevation);
        SortAnchorLocal = ReadVector2(element, "sort_anchor", SortAnchorLocal);
        SortAnchorLocal = new Vector2(
            ReadSingle(element, "sort_anchor_x", SortAnchorLocal.X),
            ReadSingle(element, "sort_anchor_y", SortAnchorLocal.Y));
        LocalSortOffset = ReadInt32(element, "local_sort_offset", LocalSortOffset);
        SortMode = ReadEnum(element, "sort_mode", SortMode);
        StableId = ReadInt32(element, "stable_id", StableId);
    }

    private int ComputeSortCoordinate(Vector2 sortAnchorWorldPosition, RenderFrame? frame)
    {
        return SortMode switch
        {
            DepthSortMode2D.None => 0,
            DepthSortMode2D.TopDownYDown => RoundSortCoordinate(sortAnchorWorldPosition.Y),
            DepthSortMode2D.TopDownYUp => RoundSortCoordinate(-sortAnchorWorldPosition.Y),
            DepthSortMode2D.ScreenProjected when frame.HasValue => ComputeProjectedSortCoordinate(sortAnchorWorldPosition, frame.Value),
            DepthSortMode2D.ScreenProjected => RoundSortCoordinate(-sortAnchorWorldPosition.Y),
            DepthSortMode2D.IsometricAxis => RoundSortCoordinate(sortAnchorWorldPosition.X - sortAnchorWorldPosition.Y),
            _ => RoundSortCoordinate(-sortAnchorWorldPosition.Y)
        };
    }

    private static int ComputeProjectedSortCoordinate(Vector2 sortAnchorWorldPosition, RenderFrame frame)
    {
        var clip = Vector4.Transform(
            new Vector4(sortAnchorWorldPosition.X, sortAnchorWorldPosition.Y, 0f, 1f),
            frame.ViewProjection);

        if (MathF.Abs(clip.W) <= float.Epsilon)
        {
            return 0;
        }

        var normalizedDeviceY = clip.Y / clip.W;
        var screenY = (-normalizedDeviceY * 0.5f + 0.5f) * frame.ViewportRect.Height + frame.ViewportRect.Y;
        return RoundSortCoordinate(screenY);
    }

    private static int RoundSortCoordinate(float value)
        => (int)MathF.Round(value * SortPrecision);

    private int GetStableId()
        => StableId != 0 ? StableId : _resolvedStableId;

    private static int CreateStableId(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);

        unchecked
        {
            const int offset = unchecked((int)2166136261);
            const int prime = 16777619;
            var hash = offset;

            for (var index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= prime;
            }

            return hash & 0x7fffffff;
        }
    }

    private static int ReadInt32(JObject element, string key, int defaultValue)
    {
        var token = element[key];
        if (token is { Type: JTokenType.Integer })
        {
            return token.Value<int>();
        }

        return token != null && int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    private static float ReadSingle(JObject element, string key, float defaultValue)
    {
        var token = element[key];
        if (token is { Type: JTokenType.Float or JTokenType.Integer })
        {
            return token.Value<float>();
        }

        return token != null && float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    private static Vector2 ReadVector2(JObject element, string key, Vector2 defaultValue)
    {
        if (element[key] is not JObject vectorNode)
        {
            return defaultValue;
        }

        return new Vector2(
            ReadSingle(vectorNode, "x", defaultValue.X),
            ReadSingle(vectorNode, "y", defaultValue.Y));
    }

    private static TEnum ReadEnum<TEnum>(JObject element, string key, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        var token = element[key];
        if (token == null)
        {
            return defaultValue;
        }

        var value = token.ToString();
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue))
        {
            return Enum.IsDefined(typeof(TEnum), numericValue)
                ? (TEnum)Enum.ToObject(typeof(TEnum), numericValue)
                : defaultValue;
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result)
            ? result
            : defaultValue;
    }
}
