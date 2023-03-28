using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Helpers;

public static class JsonHelper
{
    public static JsonProperty GetJsonPropertyByName(this JsonElement jsonElement, string key)
    {
        return jsonElement.EnumerateObject().First(x => x.Name == key);
    }

    public static void Save(this Vector2 obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
    }
    public static void Save(this Vector3 obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
        jObject.Add("z", obj.Z);
    }

    public static void Save(this Vector4 obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
        jObject.Add("z", obj.Z);
        jObject.Add("w", obj.W);
    }

    public static void Save(this Quaternion obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
        jObject.Add("z", obj.Z);
        jObject.Add("w", obj.W);
    }

    public static void Save(this Matrix obj, JObject jObject)
    {
        jObject.Add("_11", obj.M11);
        jObject.Add("_12", obj.M12);
        jObject.Add("_13", obj.M13);
        jObject.Add("_14", obj.M14);
        jObject.Add("_21", obj.M21);
        jObject.Add("_22", obj.M22);
        jObject.Add("_23", obj.M23);
        jObject.Add("_24", obj.M24);
        jObject.Add("_31", obj.M31);
        jObject.Add("_32", obj.M32);
        jObject.Add("_33", obj.M33);
        jObject.Add("_34", obj.M34);
        jObject.Add("_41", obj.M41);
        jObject.Add("_42", obj.M42);
        jObject.Add("_43", obj.M43);
        jObject.Add("_44", obj.M44);
    }
    public static void Save(this Viewport obj, JObject jObject)
    {
        jObject.Add("x", obj.X);
        jObject.Add("y", obj.Y);
        jObject.Add("width", obj.Width);
        jObject.Add("height", obj.Height);
        jObject.Add("minDepth", obj.MinDepth);
        jObject.Add("maxDepth", obj.MaxDepth);
    }

    public static string? ConvertToString(this Enum value)
    {
        return Enum.GetName(value.GetType(), value);
    }
}