using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.UI.MGUI;

public sealed class UIScreenAsset : ObjectBase
{
    public string SourceXamlFile { get; set; } = string.Empty;

    public string ThemeName { get; set; } = string.Empty;

    public Point PreviewResolution { get; set; } = new(1920, 1080);

    public List<string> ResourceFiles { get; } = new();

    /// <summary>
    /// Optional design-time data file: a JSON document naming a view-model type and giving it property
    /// values, used only by the editor preview (ADR-0038). Empty when the envelope names none, and never
    /// read by the runtime.
    /// </summary>
    public string DesignTimeDataFile { get; set; } = string.Empty;

    /// <summary>
    /// True when a screen built from this envelope alone blocks the screens below it (ADR-0078). Only read by
    /// <see cref="DataDrivenScreen"/>: a screen with its own class decides this in code.
    /// </summary>
    public bool IsModal { get; set; }

    /// <summary>
    /// Name of the XAML element a screen built from this envelope alone gives the keyboard focus to (ADR-0078).
    /// Empty leaves the focus to MGUI, except that the first usable button takes it from another screen holding it.
    /// </summary>
    public string DefaultFocus { get; set; } = string.Empty;

    /// <summary>The commands a screen built from this envelope alone offers its buttons (ADR-0078).</summary>
    public List<UIScreenCommandData> UICommands { get; } = new();

    /// <summary>
    /// Catalogue names or ids of the <c>.fnt</c> assets a screen built from this envelope alone holds for its lifetime
    /// (ADR-0079). Its XAML names their <c>face</c> in <c>FontFamily</c>.
    /// </summary>
    public List<string> Fonts { get; } = new();

    /// <summary>Which way the focus of a screen built from this envelope alone wraps at its ends (ADR-0080).</summary>
    public UIScreenFocusWrap FocusWrap { get; set; }

    /// <summary>
    /// Name of the entry of <see cref="UICommands"/> a screen built from this envelope alone runs on a Cancel nothing
    /// consumed (ADR-0080). Empty runs nothing.
    /// </summary>
    public string CancelCommand { get; set; } = string.Empty;

    /// <summary>
    /// Name of the entry of <see cref="UICommands"/> a screen built from this envelope alone runs when the keyboard or
    /// the gamepad moves the focus inside its window (ADR-0080). Empty runs nothing.
    /// </summary>
    public string MoveCommand { get; set; } = string.Empty;

    /// <summary>
    /// What <see cref="Load"/> could not read in the fields above. It reports here instead of throwing because
    /// <c>AssetLoader</c> swallows what a load throws and returns a null asset, which would lose the cause.
    /// <see cref="DataDrivenScreen"/> refuses an envelope with an entry here; other screens never read those fields.
    /// </summary>
    public List<string> CommandFormatErrors { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);

        SourceXamlFile = element["source_xaml_file"]?.GetString() ?? string.Empty;
        ThemeName = element["theme_name"]?.GetString() ?? string.Empty;
        PreviewResolution = element["preview_resolution"]?.GetPoint() ?? new Point(1920, 1080);
        DesignTimeDataFile = element["design_time_data_file"]?.GetString() ?? string.Empty;

        ResourceFiles.Clear();
        if (element["resource_files"] is JArray resourceFiles)
        {
            foreach (var resourceFile in resourceFiles)
            {
                var value = resourceFile.Value<string>();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    ResourceFiles.Add(value);
                }
            }
        }

        LoadCommandFields(element);
    }

    private void LoadCommandFields(JObject element)
    {
        CommandFormatErrors.Clear();
        UICommands.Clear();

        IsModal = false;
        if (element["is_modal"] is { } isModal)
        {
            if (isModal.Type == JTokenType.Boolean)
            {
                IsModal = isModal.GetBoolean();
            }
            else
            {
                CommandFormatErrors.Add($"'is_modal' must be a boolean, but is a {isModal.Type}.");
            }
        }

        DefaultFocus = ReadOptionalString(element, "default_focus", "'default_focus'");
        CancelCommand = ReadOptionalString(element, "cancel_command", "'cancel_command'");
        MoveCommand = ReadOptionalString(element, "move_command", "'move_command'");
        LoadFocusWrap(element);
        LoadFonts(element);

        if (element["ui_commands"] is not { } commands)
        {
            return;
        }

        if (commands is not JArray commandArray)
        {
            CommandFormatErrors.Add($"'ui_commands' must be an array, but is a {commands.Type}.");
            return;
        }

        for (int i = 0; i < commandArray.Count; i++)
        {
            if (commandArray[i] is JObject command)
            {
                UICommands.Add(ReadCommand(command, $"ui_commands[{i}]"));
            }
            else
            {
                CommandFormatErrors.Add($"ui_commands[{i}] must be an object, but is a {commandArray[i].Type}.");
            }
        }
    }

    private void LoadFocusWrap(JObject element)
    {
        FocusWrap = UIScreenFocusWrap.None;

        string name = ReadOptionalString(element, "focus_wrap", "'focus_wrap'");
        if (name.Length == 0)
        {
            return;
        }

        if (UIScreenFocusWrapNames.TryParse(name, out var focusWrap))
        {
            FocusWrap = focusWrap;
        }
        else
        {
            CommandFormatErrors.Add(
                $"'focus_wrap' is '{name}', which is neither '{UIScreenFocusWrapNames.Vertical}' nor '{UIScreenFocusWrapNames.Horizontal}'.");
        }
    }

    private void LoadFonts(JObject element)
    {
        Fonts.Clear();

        if (element["fonts"] is not { } fonts)
        {
            return;
        }

        if (fonts is not JArray fontArray)
        {
            CommandFormatErrors.Add($"'fonts' must be an array, but is a {fonts.Type}.");
            return;
        }

        for (int i = 0; i < fontArray.Count; i++)
        {
            if (fontArray[i].Type == JTokenType.String)
            {
                Fonts.Add(fontArray[i].GetString());
            }
            else
            {
                CommandFormatErrors.Add($"fonts[{i}] must be a string, but is a {fontArray[i].Type}.");
            }
        }
    }

    private UIScreenCommandData ReadCommand(JObject element, string location)
    {
        var command = new UIScreenCommandData { Name = ReadOptionalString(element, "name", $"{location}.name") };

        if (element["actions"] is not { } actions)
        {
            return command;
        }

        if (actions is not JArray actionArray)
        {
            CommandFormatErrors.Add($"{location}.actions must be an array, but is a {actions.Type}.");
            return command;
        }

        for (int i = 0; i < actionArray.Count; i++)
        {
            if (actionArray[i] is JObject action)
            {
                command.Actions.Add(ReadAction(action, $"{location}.actions[{i}]"));
            }
            else
            {
                CommandFormatErrors.Add($"{location}.actions[{i}] must be an object, but is a {actionArray[i].Type}.");
            }
        }

        return command;
    }

    private UIScreenActionData ReadAction(JObject element, string location)
    {
        var type = UIScreenActionTypeNames.Parse(ReadOptionalString(element, "type", $"{location}.type"));

        var action = new UIScreenActionData
        {
            Type = type,
            Target = ReadOptionalString(element, "target", $"{location}.target"),
            UnknownData = type == UIScreenActionType.Unknown ? (JObject)element.DeepClone() : null,
        };

        if (type == UIScreenActionType.FadeScreen)
        {
            ReadFade(element, action, location);
        }

        return action;
    }

    /// <summary>
    /// Reports what is malformed. A field left out stays null and <see cref="DataDrivenScreen"/> refuses it, as it does
    /// a missing target: a fade has no default to fall back on.
    /// </summary>
    private void ReadFade(JObject element, UIScreenActionData action, string location)
    {
        action.Layer = ReadEnum<ScreenEffectLayer>(element, "layer", location);
        action.BlendMode = ReadEnum<SpriteBlendMode>(element, "blend_mode", location);
        action.From = ReadColor(element, "from", location);
        action.To = ReadColor(element, "to", location);
        action.DurationSeconds = ReadSeconds(element, "duration_seconds", location);
    }

    /// <summary>An exact name of the enum: a number parses as an enum too.</summary>
    private TEnum? ReadEnum<TEnum>(JObject element, string key, string location)
        where TEnum : struct, Enum
    {
        if (element[key] is not { } value)
        {
            return null;
        }

        if (value.Type == JTokenType.String && Enum.IsDefined(typeof(TEnum), value.GetString()))
        {
            return Enum.Parse<TEnum>(value.GetString());
        }

        CommandFormatErrors.Add($"{location}.{key} must be one of {string.Join(", ", Enum.GetNames<TEnum>())}, but is '{value}'.");
        return null;
    }

    /// <summary><c>[r, g, b]</c>, opaque, or <c>[r, g, b, a]</c>, each from 0 to 255.</summary>
    private Color? ReadColor(JObject element, string key, string location)
    {
        if (element[key] is not { } value)
        {
            return null;
        }

        if (value is JArray { Count: 3 or 4 } channels)
        {
            var color = new byte[] { 0, 0, 0, byte.MaxValue };
            int read = 0;

            while (read < channels.Count && channels[read] is JValue { Value: >= 0L and <= 255L })
            {
                color[read] = channels[read].Value<byte>();
                read++;
            }

            if (read == channels.Count)
            {
                return new Color(color[0], color[1], color[2], color[3]);
            }
        }

        CommandFormatErrors.Add($"{location}.{key} must be [r, g, b] or [r, g, b, a], each from 0 to 255, but is {value.ToString(Newtonsoft.Json.Formatting.None)}.");
        return null;
    }

    private float? ReadSeconds(JObject element, string key, string location)
    {
        if (element[key] is not { } value)
        {
            return null;
        }

        if (value.Type is JTokenType.Float or JTokenType.Integer)
        {
            return value.Value<float>();
        }

        CommandFormatErrors.Add($"{location}.{key} must be a number, but is a {value.Type}.");
        return null;
    }

    private string ReadOptionalString(JObject element, string key, string location)
    {
        if (element[key] is not { } value)
        {
            return string.Empty;
        }

        if (value.Type == JTokenType.String)
        {
            return value.GetString();
        }

        CommandFormatErrors.Add($"{location} must be a string, but is a {value.Type}.");
        return string.Empty;
    }
}