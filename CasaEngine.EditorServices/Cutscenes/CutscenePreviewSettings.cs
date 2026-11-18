using CasaEngine.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// The editor-side memory of the cutscene preview: the world chosen for each cutscene, kept per project in
/// <c>.casaeditor/cutscene-preview.editor.json</c> (ADR-0096). It is never part of an asset.
/// </summary>
public sealed class CutscenePreviewSettings
{
    /// <summary>Name of the settings file inside the project's <c>.casaeditor</c> folder.</summary>
    public const string FileName = "cutscene-preview.editor.json";

    private const string WorldsKey = "worlds";

    private readonly Dictionary<Guid, Guid> _worldByCutscene = [];

    /// <summary>The world remembered for a cutscene, or <see cref="Guid.Empty"/> when none.</summary>
    public Guid GetWorldId(Guid cutsceneAssetId)
        => _worldByCutscene.TryGetValue(cutsceneAssetId, out Guid worldId) ? worldId : Guid.Empty;

    /// <summary>Remembers the world of a cutscene; <see cref="Guid.Empty"/> forgets it.</summary>
    public void SetWorldId(Guid cutsceneAssetId, Guid worldId)
    {
        if (worldId == Guid.Empty)
        {
            _worldByCutscene.Remove(cutsceneAssetId);
        }
        else
        {
            _worldByCutscene[cutsceneAssetId] = worldId;
        }
    }

    /// <summary>The path of the settings file of a project folder.</summary>
    public static string GetPath(string projectDirectory)
        => Path.Combine(projectDirectory, ".casaeditor", FileName);

    /// <summary>
    /// Loads the settings of a project folder. A missing file gives empty settings; an unreadable one gives empty
    /// settings and a warning in the log.
    /// </summary>
    public static CutscenePreviewSettings Load(string projectDirectory)
    {
        string path = GetPath(projectDirectory);
        var settings = new CutscenePreviewSettings();
        if (!File.Exists(path))
        {
            return settings;
        }

        try
        {
            var root = JObject.Parse(File.ReadAllText(path));
            if (root[WorldsKey] is JObject worlds)
            {
                foreach (JProperty property in worlds.Properties())
                {
                    if (Guid.TryParse(property.Name, out Guid cutsceneId)
                        && property.Value.Type == JTokenType.String
                        && Guid.TryParse((string)property.Value, out Guid worldId))
                    {
                        settings.SetWorldId(cutsceneId, worldId);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Logs.WriteWarning($"Cutscene preview: the settings file '{path}' could not be read, empty settings are used: {exception.Message}");
            return new CutscenePreviewSettings();
        }

        return settings;
    }

    /// <summary>Writes the settings in the <c>.casaeditor</c> folder of a project, creating it when needed.</summary>
    public void Save(string projectDirectory)
    {
        string path = GetPath(projectDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var worlds = new JObject();
        foreach (KeyValuePair<Guid, Guid> pair in _worldByCutscene.OrderBy(p => p.Key))
        {
            worlds[pair.Key.ToString()] = pair.Value.ToString();
        }

        File.WriteAllText(path, new JObject { [WorldsKey] = worlds }.ToString(Formatting.Indented));
    }
}
