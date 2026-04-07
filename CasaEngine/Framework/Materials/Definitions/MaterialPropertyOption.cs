namespace CasaEngine.Framework.Materials.Definitions;

public sealed record MaterialPropertyOption
{
    public MaterialPropertyOption(string value, string displayName, string description = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Value = value;
        DisplayName = displayName;
        Description = description;
    }

    public string Value { get; }
    public string DisplayName { get; }
    public string Description { get; }
}