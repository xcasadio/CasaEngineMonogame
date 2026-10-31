using CasaEngine.Core.Serialization;
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
    /// Empty leaves the focus to MGUI.
    /// </summary>
    public string DefaultFocus { get; set; } = string.Empty;

    /// <summary>The commands a screen built from this envelope alone offers its buttons (ADR-0078).</summary>
    public List<UIScreenCommandData> UICommands { get; } = new();

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

        return new UIScreenActionData
        {
            Type = type,
            Target = ReadOptionalString(element, "target", $"{location}.target"),
            UnknownData = type == UIScreenActionType.Unknown ? (JObject)element.DeepClone() : null,
        };
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