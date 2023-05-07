using System;

namespace CasaEngine.WpfControls;

public static class TypeExtensions
{
    public static bool IsNullable(this Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return Nullable.GetUnderlyingType(type) != null;
    }

    public static object Default(this Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}