using System.Text;

namespace CasaEngine.Framework.Assets;

/// <summary>
/// A script object (an entity or world gameplay proxy, a player controller, a gameplay mode, a design-time
/// view model) could not be created from the type name written in the data: the full name is unknown in the
/// registered gameplay assemblies, ambiguous, or names a type of the wrong kind. The message carries
/// everything needed to fix the data: the key, the value, the file that holds it, the assemblies that were
/// searched and a "did you mean" hint (ADR-0075).
/// </summary>
public sealed class ScriptTypeResolutionException : Exception
{
    private string _fileName;

    /// <summary>The data key that held the name, for example <c>script_class_name</c>.</summary>
    public string Key { get; }

    /// <summary>The type name as written in the data (possibly empty).</summary>
    public string Value { get; }

    /// <summary>Why the name did not resolve.</summary>
    public string Reason { get; }

    /// <summary>The registered script assemblies that were searched, or a note saying there are none.</summary>
    public string SearchedAssemblies { get; }

    /// <summary>A "did you mean" suggestion, or an empty string.</summary>
    public string Hint { get; }

    /// <summary>
    /// The file that holds the name, or null when unknown. A creation site that does not know its file leaves it
    /// null and an enclosing owner (the world, for an inline entity) completes it with <see cref="CompleteFileName"/>.
    /// </summary>
    public string FileName => _fileName;

    public ScriptTypeResolutionException(string key, string value, string reason, string searchedAssemblies, string hint)
        : base(reason)
    {
        Key = key ?? string.Empty;
        Value = value ?? string.Empty;
        Reason = reason ?? string.Empty;
        SearchedAssemblies = searchedAssemblies ?? string.Empty;
        Hint = hint ?? string.Empty;
    }

    /// <summary>Sets the file when none is known yet; a file already known is kept.</summary>
    public void CompleteFileName(string fileName)
    {
        if (string.IsNullOrEmpty(_fileName) && !string.IsNullOrEmpty(fileName))
        {
            _fileName = fileName;
        }
    }

    public override string Message => BuildMessage(_fileName);

    internal string BuildMessage(string fileNameOverride)
    {
        var builder = new StringBuilder();
        builder.Append("Script type '").Append(Value).Append("' (key '").Append(Key).Append("', file '")
            .Append(string.IsNullOrEmpty(fileNameOverride) ? "unknown file" : fileNameOverride)
            .Append("') could not be resolved: ").Append(Reason)
            .Append(" Searched assemblies: ").Append(SearchedAssemblies).Append('.');

        if (Hint.Length > 0)
        {
            builder.Append(' ').Append(Hint);
        }

        return builder.ToString();
    }
}
