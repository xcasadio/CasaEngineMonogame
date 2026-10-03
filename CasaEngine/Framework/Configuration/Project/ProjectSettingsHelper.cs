using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Application;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Configuration.Project;

public static class ProjectSettingsHelper
{
    public static void Load(string fileName, EngineRuntimeContext runtimeContext = null)
    {
        var context = runtimeContext ?? GameSettings.CreateRuntimeContext();
        var projectSettings = context.ProjectSettings;
        context.ProjectPath = Path.GetDirectoryName(fileName) ?? EngineEnvironment.ResolveProjectPath(null);
        EngineEnvironment.ProjectPath = context.ProjectPath;

        var rootElement = JObject.Parse(File.ReadAllText(fileName));

        projectSettings.WindowTitle = rootElement["WindowTitle"].GetString();
        projectSettings.ProjectName = rootElement["ProjectName"].GetString();
        projectSettings.FirstScreenName = rootElement["FirstScreenName"].GetString();
        projectSettings.AllowUserResizing = rootElement["AllowUserResizing"]?.GetBoolean() ?? projectSettings.AllowUserResizing;
        projectSettings.IsFixedTimeStep = rootElement["IsFixedTimeStep"]?.GetBoolean() ?? projectSettings.IsFixedTimeStep;
        projectSettings.IsMouseVisible = rootElement["IsMouseVisible"]?.GetBoolean() ?? projectSettings.IsMouseVisible;

        projectSettings.FirstWorldLoaded = rootElement["FirstWorldLoaded"].GetString();
        projectSettings.GameplayDllName = rootElement["GameplayDllName"].GetString();
        projectSettings.GameplayCsprojName = rootElement["GameplayCsprojName"]?.GetString() ?? string.Empty;
        projectSettings.DialogueScreenAsset = rootElement["DialogueScreenAsset"]?.GetString() ?? string.Empty;
        projectSettings.ExternalToolsDirectory = rootElement["ExternalToolsDirectory"]?.GetString() ?? projectSettings.ExternalToolsDirectory;

        // Absent means false, not "keep the previous value": otherwise opening an unmuted
        // project right after a muted one would leave the mute on.
        projectSettings.IsAudioMuted = rootElement["IsAudioMuted"]?.GetBoolean() ?? false;

        // Same rule: absent means no virtual resolution (the full-window view), never the previous project's.
        projectSettings.VirtualResolution = ReadVirtualResolution(rootElement["VirtualResolution"]);

#if !FINAL
        projectSettings.DebugIsFullScreen = rootElement["DebugIsFullScreen"]?.GetBoolean() ?? projectSettings.DebugIsFullScreen;
        projectSettings.DebugHeight = rootElement["DebugHeight"]?.GetInt32() ?? projectSettings.DebugHeight;
        projectSettings.DebugWidth = rootElement["DebugWidth"]?.GetInt32() ?? projectSettings.DebugWidth;
        projectSettings.VSyncEnabled = rootElement["VSyncEnabled"]?.GetBoolean() ?? projectSettings.VSyncEnabled;
#endif

        if (!string.IsNullOrWhiteSpace(projectSettings.GameplayDllName))
        {
            GameSettings.AssemblyManager.Load(projectSettings.GameplayDllName);
        }

        var assetInfoFileName = Path.Combine(Path.GetDirectoryName(fileName), "AssetInfos.json");
        if (!File.Exists(assetInfoFileName))
        {
            return;
        }

        AssetCatalog.Load(assetInfoFileName);
    }

    private static VirtualResolutionSettings ReadVirtualResolution(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token is not JObject declaration)
        {
            throw new InvalidDataException("Project setting 'VirtualResolution' must be an object with Width, Height and an optional Mode.");
        }

        int width = ReadPositiveDimension(declaration, "Width");
        int height = ReadPositiveDimension(declaration, "Height");

        var mode = VirtualResolutionMode.IntegerFit;
        string modeName = (string)declaration["Mode"];
        if (modeName != null && (!Enum.TryParse(modeName, ignoreCase: false, out mode) || !Enum.IsDefined(mode)))
        {
            throw new InvalidDataException(
                $"Project setting 'VirtualResolution.Mode' is '{modeName}'; the supported mode is '{nameof(VirtualResolutionMode.IntegerFit)}'.");
        }

        return new VirtualResolutionSettings { Width = width, Height = height, Mode = mode };
    }

    private static int ReadPositiveDimension(JObject declaration, string key)
    {
        int? value = (int?)declaration[key];
        if (value is not > 0)
        {
            throw new InvalidDataException($"Project setting 'VirtualResolution.{key}' must be a positive integer.");
        }

        return value.Value;
    }

    public static void Save(string fileName, ProjectSettings projectSettings = null)
    {
        var settings = projectSettings ?? GameSettings.ProjectSettings;
        string directoryName = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrWhiteSpace(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        var rootElement = new JObject
        {
            ["WindowTitle"] = settings.WindowTitle,
            ["ProjectName"] = settings.ProjectName,
            ["FirstScreenName"] = settings.FirstScreenName,
            ["AllowUserResizing"] = settings.AllowUserResizing,
            ["IsFixedTimeStep"] = settings.IsFixedTimeStep,
            ["IsMouseVisible"] = settings.IsMouseVisible,
            ["FirstWorldLoaded"] = settings.FirstWorldLoaded,
            ["GameplayDllName"] = settings.GameplayDllName,
            ["ExternalToolsDirectory"] = settings.ExternalToolsDirectory,
        };

        if (!string.IsNullOrWhiteSpace(settings.GameplayCsprojName))
        {
            rootElement["GameplayCsprojName"] = settings.GameplayCsprojName;
        }

        if (!string.IsNullOrWhiteSpace(settings.DialogueScreenAsset))
        {
            rootElement["DialogueScreenAsset"] = settings.DialogueScreenAsset;
        }

        if (settings.IsAudioMuted)
        {
            rootElement["IsAudioMuted"] = true;
        }

        if (settings.VirtualResolution != null)
        {
            rootElement["VirtualResolution"] = new JObject
            {
                ["Width"] = settings.VirtualResolution.Width,
                ["Height"] = settings.VirtualResolution.Height,
                ["Mode"] = settings.VirtualResolution.Mode.ToString(),
            };
        }

#if !FINAL
        rootElement["DebugIsFullScreen"] = settings.DebugIsFullScreen;
        rootElement["DebugHeight"] = settings.DebugHeight;
        rootElement["DebugWidth"] = settings.DebugWidth;
        rootElement["VSyncEnabled"] = settings.VSyncEnabled;
#endif

        File.WriteAllText(fileName, rootElement.ToString());
    }
}