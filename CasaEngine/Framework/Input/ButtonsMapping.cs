using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Input;

public class ButtonsMapping : ObjectBase
{
    public List<InputMapping> Buttons { get; } = new();

    /// <summary>
    /// How long a direction stays held before it repeats, in milliseconds (ADR-0080). Null when the file does not say:
    /// the reader then applies its own default, and a save does not write the key.
    /// </summary>
    public int? RepeatInitialDelayMs { get; set; }

    /// <summary>
    /// The time between two repeats of a held direction, in milliseconds (ADR-0080). Null when the file does not say:
    /// the reader then applies its own default, and a save does not write the key.
    /// </summary>
    public int? RepeatIntervalMs { get; set; }

    public override void Load(JObject element)
    {
        base.Load(element);

        var buttons = element.GetElements("buttons", t =>
        {
            var button = new InputMapping();
            button.Load((JObject)t);
            return button;
        });

        Buttons.AddRange(buttons);

        RepeatInitialDelayMs = ReadOptionalMilliseconds(element, "repeat_initial_delay_ms");
        RepeatIntervalMs = ReadOptionalMilliseconds(element, "repeat_interval_ms");
    }

    // Only a JSON integer: a "120" string would otherwise load as a mapping that silently keeps the default.
    private static int? ReadOptionalMilliseconds(JObject element, string key)
    {
        if (element[key] is not { } value)
        {
            return null;
        }

        if (value.Type != JTokenType.Integer)
        {
            throw new InvalidDataException($"'{key}' must be an integer number of milliseconds, but is a {value.Type}.");
        }

        return value.GetInt32();
    }

}
