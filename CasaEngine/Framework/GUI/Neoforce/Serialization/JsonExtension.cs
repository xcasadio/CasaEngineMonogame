
using CasaEngine.Core.Serialization;
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




    public static Margins GetMargins(this JToken element)
    {
        return new Margins
        {
            Left = element["l"].GetByte(),
            Right = element["r"].GetByte(),
            Top = element["t"].GetByte(),
            Bottom = element["b"].GetByte()
        };
    }

    public static Anchors GetAnchors(this JToken element)
    {
        return Enum.Parse<Anchors>(element.GetString(), true);
    }
}