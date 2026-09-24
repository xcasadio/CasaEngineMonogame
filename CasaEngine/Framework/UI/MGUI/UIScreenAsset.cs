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
    }
}