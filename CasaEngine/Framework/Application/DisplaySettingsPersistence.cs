using CasaEngine.Core.Logging;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Application;

public static class DisplaySettingsPersistence
{
    public static DisplaySettings Load(string fileName, DisplaySettings fallbackSettings)
    {
        try
        {
            if (!File.Exists(fileName))
            {
                return fallbackSettings;
            }

            JObject rootElement = JObject.Parse(File.ReadAllText(fileName));
            int width = rootElement["Width"]?.Value<int>() ?? fallbackSettings.Width;
            int height = rootElement["Height"]?.Value<int>() ?? fallbackSettings.Height;
            bool isFullScreen = rootElement["IsFullScreen"]?.Value<bool>() ?? fallbackSettings.IsFullScreen;
            bool isVSyncEnabled = rootElement["IsVSyncEnabled"]?.Value<bool>() ?? fallbackSettings.IsVSyncEnabled;
            return new DisplaySettings(width, height, isFullScreen, isVSyncEnabled);
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            return fallbackSettings;
        }
    }

    public static void Save(string fileName, DisplaySettings displaySettings)
    {
        string directoryName = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrWhiteSpace(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        var rootElement = new JObject
        {
            ["Width"] = displaySettings.Width,
            ["Height"] = displaySettings.Height,
            ["IsFullScreen"] = displaySettings.IsFullScreen,
            ["IsVSyncEnabled"] = displaySettings.IsVSyncEnabled,
        };

        File.WriteAllText(fileName, rootElement.ToString());
    }
}