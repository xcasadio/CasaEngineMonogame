using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices;

/// <summary>
/// Writes the properties marked with <see cref="SerializedPropertyAttribute"/> in the JSON form that
/// <see cref="SerializedProperties.Load"/> reads back (ADR-0071).
/// </summary>
internal static class EditorSerializedPropertyWriter
{
    /// <summary>Adds (or replaces) the key of every marked property of <paramref name="source"/> in <paramref name="node"/>.</summary>
    public static void Save(object source, JObject node)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(node);

        var members = SerializedProperties.GetMembers(source.GetType());
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            node[member.JsonName] = ToJson(member.Property.GetValue(source), member.Property.PropertyType);
        }
    }

    public static JToken ToJson(object value, Type type)
    {
        if (value == null)
        {
            return JValue.CreateNull();
        }

        if (type.IsEnum)
        {
            return new JValue(value.ToString());
        }

        if (type == typeof(Guid))
        {
            return new JValue(((Guid)value).ToString());
        }

        var node = new JObject();
        switch (value)
        {
            case Vector2 vector2:
                vector2.Save(node);
                return node;
            case Vector3 vector3:
                vector3.Save(node);
                return node;
            case Vector4 vector4:
                vector4.Save(node);
                return node;
            case Quaternion quaternion:
                quaternion.Save(node);
                return node;
            case Color color:
                color.Save(node);
                return node;
            default:
                return new JValue(value);
        }
    }
}
