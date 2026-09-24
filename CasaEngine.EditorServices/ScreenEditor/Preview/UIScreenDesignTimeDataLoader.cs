using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI.MGUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.ScreenEditor.Preview;

/// <summary>
/// Result of <see cref="UIScreenDesignTimeDataLoader.Load"/>: either a populated view-model instance to use as
/// the preview window's data context, or an error message to report next to the preview (ADR-0038,
/// "Design-time data"). Never both, and never throws -- every failure is captured here instead.
/// </summary>
public sealed class UIScreenDesignTimeDataResult
{
    /// <summary>The <c>.uiscreen</c> envelope named no design-time data file: there is nothing to load, and
    /// this is not an error.</summary>
    public static readonly UIScreenDesignTimeDataResult None = new(null, null);

    /// <summary>The populated view-model instance to use as the preview window's data context, or null when
    /// the asset named no design-time data file or loading it failed.</summary>
    public object? DataContext { get; }

    /// <summary>A message describing why <see cref="DataContext"/> is null, or null when there was nothing to
    /// load or loading succeeded.</summary>
    public string? ErrorMessage { get; }

    private UIScreenDesignTimeDataResult(object? dataContext, string? errorMessage)
    {
        DataContext = dataContext;
        ErrorMessage = errorMessage;
    }

    internal static UIScreenDesignTimeDataResult Success(object dataContext) => new(dataContext, null);

    internal static UIScreenDesignTimeDataResult Error(string message) => new(null, message);
}

/// <summary>
/// Loads a screen's optional design-time data file (ADR-0038, "Design-time data"; <c>.uiscreen</c> field
/// <c>design_time_data_file</c>, see <c>docs/editor/ui-screen-editor/screen-authoring-conventions.md</c>): a
/// JSON document naming a view-model type and giving it property values. The named type is resolved through
/// <see cref="ElementFactory"/> -- so it must be a public type with a public parameterless constructor, found
/// either in the engine or in the project's loaded gameplay assembly (<see cref="ElementFactory.RegisterScriptAssembly"/>)
/// -- and by its simple (non-namespace-qualified) name, the same convention <see cref="ElementFactory"/> uses
/// everywhere else. A missing or invalid file, an unresolved type, or a population failure never throws: they
/// are logged with context and reported through <see cref="UIScreenDesignTimeDataResult.ErrorMessage"/>, and
/// the caller renders its preview without a data context.
/// </summary>
public static class UIScreenDesignTimeDataLoader
{
    public static UIScreenDesignTimeDataResult Load(UIScreenAsset asset, string assetFilePath)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (string.IsNullOrWhiteSpace(asset.DesignTimeDataFile))
        {
            return UIScreenDesignTimeDataResult.None;
        }

        string dataFilePath;
        try
        {
            dataFilePath = ResolveDesignTimeDataPath(asset.DesignTimeDataFile, assetFilePath);
        }
        catch (FileNotFoundException ex)
        {
            return Fail(ex.Message);
        }
        catch (Exception ex)
        {
            // An unusable path (invalid characters, access denied...) is a data error too.
            return Fail($"Design-time data file '{asset.DesignTimeDataFile}' could not be located: {ex.Message}");
        }

        JObject root;
        try
        {
            root = JObject.Parse(File.ReadAllText(dataFilePath));
        }
        catch (Exception ex)
        {
            return Fail($"Design-time data file '{dataFilePath}' could not be read as a JSON object: {ex.Message}");
        }

        var typeToken = root["view_model_type"];
        if (typeToken == null || typeToken.Type == JTokenType.Null)
        {
            return Fail($"Design-time data file '{dataFilePath}' is missing 'view_model_type'.");
        }

        if (typeToken.Type != JTokenType.String)
        {
            return Fail($"Design-time data file '{dataFilePath}' has a 'view_model_type' that is not a string ({typeToken.Type}).");
        }

        var typeName = typeToken.Value<string>();
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return Fail($"Design-time data file '{dataFilePath}' is missing 'view_model_type'.");
        }

        var valuesToken = root["values"];
        if (valuesToken != null && valuesToken.Type != JTokenType.Null && valuesToken is not JObject)
        {
            return Fail($"Design-time data file '{dataFilePath}' has a 'values' that is not a JSON object ({valuesToken.Type}).");
        }

        object instance;
        try
        {
            instance = ElementFactory.Create<object>(typeName);
        }
        catch (Exception ex)
        {
            return Fail($"Design-time data file '{dataFilePath}' names view model type '{typeName}', which could not be instantiated: {ex.Message}");
        }

        if (instance == null)
        {
            return Fail($"Design-time data file '{dataFilePath}' names view model type '{typeName}', which was not found in the engine or the project's gameplay assembly.");
        }

        if (root["values"] is JObject values)
        {
            try
            {
                JsonConvert.PopulateObject(values.ToString(), instance);
            }
            catch (Exception ex)
            {
                return Fail($"Design-time data file '{dataFilePath}' could not populate view model type '{typeName}': {ex.Message}");
            }
        }

        return UIScreenDesignTimeDataResult.Success(instance);
    }

    private static UIScreenDesignTimeDataResult Fail(string message)
    {
        Logs.WriteWarning($"UIScreenDesignTimeDataLoader: {message}");
        return UIScreenDesignTimeDataResult.Error(message);
    }

    /// <summary>
    /// Resolves a design-time data file path the same way <c>UIScreenLoader.ResolveSourceXamlPath</c> resolves
    /// <see cref="UIScreenAsset.SourceXamlFile"/>: an absolute path as given, otherwise relative to the
    /// envelope, otherwise relative to the project. Every candidate must exist on disk to be returned.
    /// </summary>
    private static string ResolveDesignTimeDataPath(string designTimeDataFile, string assetFilePath)
    {
        if (Path.IsPathRooted(designTimeDataFile))
        {
            if (!File.Exists(designTimeDataFile))
            {
                throw new FileNotFoundException(
                    $"Design-time data file was not found at its absolute path '{designTimeDataFile}'.",
                    designTimeDataFile);
            }

            return designTimeDataFile;
        }

        string relativeToAsset = null;
        var assetDirectory = string.IsNullOrWhiteSpace(assetFilePath) ? null : Path.GetDirectoryName(assetFilePath);

        if (!string.IsNullOrWhiteSpace(assetDirectory))
        {
            relativeToAsset = Path.GetFullPath(Path.Combine(assetDirectory, designTimeDataFile));

            if (File.Exists(relativeToAsset))
            {
                return relativeToAsset;
            }
        }

        var projectPath = EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath);
        var relativeToProject = Path.GetFullPath(Path.Combine(projectPath, designTimeDataFile));

        if (File.Exists(relativeToProject))
        {
            return relativeToProject;
        }

        throw new FileNotFoundException(
            $"Design-time data file '{designTimeDataFile}' was not found. Tried " +
            $"'{relativeToAsset ?? "(the asset has no directory)"}' and '{relativeToProject}'.",
            designTimeDataFile);
    }
}
