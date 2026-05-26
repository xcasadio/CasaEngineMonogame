using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Materials.Runtime;

public sealed class MaterialValue : IEquatable<MaterialValue>
{
    private readonly float _floatValue;
    private readonly int _integerValue;
    private readonly bool _booleanValue;
    private readonly Color _colorValue;
    private readonly Vector2 _vector2Value;
    private readonly Vector3 _vector3Value;
    private readonly Vector4 _vector4Value;
    private readonly Guid _textureId;
    private readonly string _textValue;

    private MaterialValue(
        MaterialPropertyType type,
        float floatValue = default,
        int integerValue = default,
        bool booleanValue = default,
        Color colorValue = default,
        Vector2 vector2Value = default,
        Vector3 vector3Value = default,
        Vector4 vector4Value = default,
        Guid textureId = default,
        string textValue = null)
    {
        Type = type;
        _floatValue = floatValue;
        _integerValue = integerValue;
        _booleanValue = booleanValue;
        _colorValue = colorValue;
        _vector2Value = vector2Value;
        _vector3Value = vector3Value;
        _vector4Value = vector4Value;
        _textureId = textureId;
        _textValue = textValue;
    }

    public MaterialPropertyType Type { get; }

    public static MaterialValue FromFloat(float value)
        => new(MaterialPropertyType.Float, floatValue: value);

    public static MaterialValue FromInteger(int value)
        => new(MaterialPropertyType.Integer, integerValue: value);

    public static MaterialValue FromBoolean(bool value)
        => new(MaterialPropertyType.Boolean, booleanValue: value);

    public static MaterialValue FromColor(Color value)
        => new(MaterialPropertyType.Color, colorValue: value);

    public static MaterialValue FromVector2(Vector2 value)
        => new(MaterialPropertyType.Vector2, vector2Value: value);

    public static MaterialValue FromVector3(Vector3 value)
        => new(MaterialPropertyType.Vector3, vector3Value: value);

    public static MaterialValue FromVector4(Vector4 value)
        => new(MaterialPropertyType.Vector4, vector4Value: value);

    public static MaterialValue FromTextureId(Guid value)
        => new(MaterialPropertyType.Texture, textureId: value);

    public static MaterialValue FromEnum(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new MaterialValue(MaterialPropertyType.Enum, textValue: value);
    }

    public static MaterialValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new MaterialValue(MaterialPropertyType.String, textValue: value);
    }

    public static MaterialValue FromObject(MaterialPropertyType type, object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return type switch
        {
            MaterialPropertyType.Float when value is float typedValue => FromFloat(typedValue),
            MaterialPropertyType.Integer when value is int typedValue => FromInteger(typedValue),
            MaterialPropertyType.Boolean when value is bool typedValue => FromBoolean(typedValue),
            MaterialPropertyType.Color when value is Color typedValue => FromColor(typedValue),
            MaterialPropertyType.Vector2 when value is Vector2 typedValue => FromVector2(typedValue),
            MaterialPropertyType.Vector3 when value is Vector3 typedValue => FromVector3(typedValue),
            MaterialPropertyType.Vector4 when value is Vector4 typedValue => FromVector4(typedValue),
            MaterialPropertyType.Texture when value is Guid typedValue => FromTextureId(typedValue),
            MaterialPropertyType.Enum when value is string typedValue => FromEnum(typedValue),
            MaterialPropertyType.String when value is string typedValue => FromString(typedValue),
            _ => throw new ArgumentException(
                $"Value '{value}' is not compatible with material value type '{type}'.",
                nameof(value)),
        };
    }

    public bool TryGetFloat(out float value)
    {
        if (Type == MaterialPropertyType.Float)
        {
            value = _floatValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetInteger(out int value)
    {
        if (Type == MaterialPropertyType.Integer)
        {
            value = _integerValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetBoolean(out bool value)
    {
        if (Type == MaterialPropertyType.Boolean)
        {
            value = _booleanValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetColor(out Color value)
    {
        if (Type == MaterialPropertyType.Color)
        {
            value = _colorValue;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetVector2(out Vector2 value)
    {
        if (Type == MaterialPropertyType.Vector2)
        {
            value = _vector2Value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetVector3(out Vector3 value)
    {
        if (Type == MaterialPropertyType.Vector3)
        {
            value = _vector3Value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetVector4(out Vector4 value)
    {
        if (Type == MaterialPropertyType.Vector4)
        {
            value = _vector4Value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetTextureId(out Guid value)
    {
        if (Type == MaterialPropertyType.Texture)
        {
            value = _textureId;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetEnum(out string value)
    {
        if (Type == MaterialPropertyType.Enum)
        {
            value = _textValue;
            return true;
        }

        value = null;
        return false;
    }

    public bool TryGetString(out string value)
    {
        if (Type == MaterialPropertyType.String)
        {
            value = _textValue;
            return true;
        }

        value = null;
        return false;
    }

    public object ToObject()
        => Type switch
        {
            MaterialPropertyType.Float => _floatValue,
            MaterialPropertyType.Integer => _integerValue,
            MaterialPropertyType.Boolean => _booleanValue,
            MaterialPropertyType.Color => _colorValue,
            MaterialPropertyType.Vector2 => _vector2Value,
            MaterialPropertyType.Vector3 => _vector3Value,
            MaterialPropertyType.Vector4 => _vector4Value,
            MaterialPropertyType.Texture => _textureId,
            MaterialPropertyType.Enum => _textValue!,
            MaterialPropertyType.String => _textValue!,
            _ => throw new InvalidOperationException($"Unsupported material value type '{Type}'."),
        };

    public bool IsCompatibleWith(MaterialPropertyDefinition definition, out string validationError)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (Type != definition.ValueType)
        {
            validationError =
                $"Material value type '{Type}' is not compatible with property '{definition.Key}' of type '{definition.ValueType}'.";
            return false;
        }

        switch (Type)
        {
            case MaterialPropertyType.Float:
                return ValidateNumericRange(definition, _floatValue, out validationError);

            case MaterialPropertyType.Integer:
                return ValidateNumericRange(definition, _integerValue, out validationError);

            case MaterialPropertyType.Enum:
                return ValidateEnumValue(definition, _textValue!, out validationError);

            default:
                validationError = null;
                return true;
        }
    }

    public override string ToString()
        => ToObject().ToString() ?? string.Empty;

    public bool Equals(MaterialValue other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Type != other.Type)
        {
            return false;
        }

        return Type switch
        {
            MaterialPropertyType.Float => _floatValue.Equals(other._floatValue),
            MaterialPropertyType.Integer => _integerValue == other._integerValue,
            MaterialPropertyType.Boolean => _booleanValue == other._booleanValue,
            MaterialPropertyType.Color => _colorValue.Equals(other._colorValue),
            MaterialPropertyType.Vector2 => _vector2Value.Equals(other._vector2Value),
            MaterialPropertyType.Vector3 => _vector3Value.Equals(other._vector3Value),
            MaterialPropertyType.Vector4 => _vector4Value.Equals(other._vector4Value),
            MaterialPropertyType.Texture => _textureId.Equals(other._textureId),
            MaterialPropertyType.Enum => string.Equals(_textValue, other._textValue, StringComparison.Ordinal),
            MaterialPropertyType.String => string.Equals(_textValue, other._textValue, StringComparison.Ordinal),
            _ => false,
        };
    }

    public override bool Equals(object obj)
        => obj is MaterialValue other && Equals(other);

    public override int GetHashCode()
        => Type switch
        {
            MaterialPropertyType.Float => HashCode.Combine(Type, _floatValue),
            MaterialPropertyType.Integer => HashCode.Combine(Type, _integerValue),
            MaterialPropertyType.Boolean => HashCode.Combine(Type, _booleanValue),
            MaterialPropertyType.Color => HashCode.Combine(Type, _colorValue),
            MaterialPropertyType.Vector2 => HashCode.Combine(Type, _vector2Value),
            MaterialPropertyType.Vector3 => HashCode.Combine(Type, _vector3Value),
            MaterialPropertyType.Vector4 => HashCode.Combine(Type, _vector4Value),
            MaterialPropertyType.Texture => HashCode.Combine(Type, _textureId),
            MaterialPropertyType.Enum => HashCode.Combine(Type, _textValue),
            MaterialPropertyType.String => HashCode.Combine(Type, _textValue),
            _ => HashCode.Combine(Type),
        };

    private static bool ValidateNumericRange(
        MaterialPropertyDefinition definition,
        float value,
        out string validationError)
    {
        if (definition.MinValue.HasValue && value < definition.MinValue.Value)
        {
            validationError =
                $"Material value '{value}' is below the minimum '{definition.MinValue.Value}' for property '{definition.Key}'.";
            return false;
        }

        if (definition.MaxValue.HasValue && value > definition.MaxValue.Value)
        {
            validationError =
                $"Material value '{value}' is above the maximum '{definition.MaxValue.Value}' for property '{definition.Key}'.";
            return false;
        }

        validationError = null;
        return true;
    }

    private static bool ValidateEnumValue(
        MaterialPropertyDefinition definition,
        string value,
        out string validationError)
    {
        for (int i = 0; i < definition.Options.Count; i++)
        {
            if (string.Equals(definition.Options[i].Value, value, StringComparison.OrdinalIgnoreCase))
            {
                validationError = null;
                return true;
            }
        }

        validationError =
            $"Material value '{value}' is not declared for enum property '{definition.Key}'.";
        return false;
    }
}