using System;
using System.Text.Json;
using Microsoft.Xna.Framework;
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





    public static Rectangle GetRectangle(this JsonElement element)
    {
        return new Rectangle
        {
            X = element.GetProperty("x").GetInt32(),
            Y = element.GetProperty("y").GetInt32(),
            Width = element.GetProperty("w").GetInt32(),
            Height = element.GetProperty("h").GetInt32()
        };
    }

    public static Color GetColor(this JsonElement element)
    {
        return new Color
        {
            R = element.GetProperty("r").GetByte(),
            G = element.GetProperty("g").GetByte(),
            B = element.GetProperty("b").GetByte(),
            A = element.GetProperty("a").GetByte()
        };
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


    public static T GetEnum<T>(this JsonElement element) where T : struct
    {
        return Enum.Parse<T>(element.GetString(), true);
    }
}