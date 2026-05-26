namespace CasaEngine.Core.Serialization;

public static class EnumSerializationExtensions
{
    public static string ConvertToString(this Enum value)
    {
        return Enum.GetName(value.GetType(), value);
    }
}