using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace TomShane.Neoforce.Controls.Serialization;

public static class JsonExtension
{
    public static void Add(this JObject node, string name, Color color)
    {
        node.Add(name, new JObject
        {
            { "r", color.R },
            { "g", color.G },
            { "b", color.B },
            { "a", color.A }
        });
    }

    public static void Add(this JObject node, string name, Rectangle rectangle)
    {
        node.Add(name, new JObject
        {
            { "x", rectangle.X },
            { "y", rectangle.Y },
            { "w", rectangle.Width },
            { "h", rectangle.Height }
        });
    }

    public static void Add(this JObject node, string name, Margins margins)
    {
        node.Add(name, new JObject
        {
            { "l", margins.Left },
            { "r", margins.Right },
            { "t", margins.Top },
            { "b", margins.Bottom }
        });
    }

    public static void Add(this JObject node, string name, Enum value)
    {
        node.Add(name, Enum.GetName(value.GetType(), value));
    }




    public static Margins GetMargins(this JsonElement element)
    {
        return new Margins
        {
            Left = element.GetProperty("l").GetByte(),
            Right = element.GetProperty("r").GetByte(),
            Top = element.GetProperty("t").GetByte(),
            Bottom = element.GetProperty("b").GetByte()
        };
    }

    public static Anchors GetAnchors(this JsonElement element)
    {
        return Enum.Parse<Anchors>(element.GetString(), true);
    }
}