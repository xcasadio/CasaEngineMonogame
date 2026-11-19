using System;
using System.IO;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Editor.Remote;

/// <summary>The asset types the remote commands edit (ADR-0093 D1), by their catalog type (the file extension).</summary>
internal static class EditorRemoteAssetTypes
{
    public const string Material = "material";
    public const string Particle = "particle";
    public const string UIScreen = "uiscreen";
    public const string Sound = "sound";
    public const string Entity = "entity";

    /// <summary>The five types <c>asset.open</c> accepts (plan P7, ADR-0129); a world goes through <c>world.open</c>.</summary>
    public static bool IsOpenable(string type) => type is Material or Particle or UIScreen or Sound or Entity;

    /// <summary>
    /// The four types the automation flag <c>--open-asset</c> routes through <c>asset.open</c>; an <c>.entity</c> keeps the
    /// editor's own route (plan P4).
    /// </summary>
    public static bool IsAutomationOpenable(string type) => type is Material or Particle or UIScreen or Sound;

    public static string Extension(string type) => "." + type;
}

/// <summary>A catalogued asset a remote command acts on, checked to be inside the project.</summary>
internal readonly record struct RemoteAssetReference(AssetInfo Info, string FullPath, string Type);

/// <summary>
/// Resolution of an asset argument (ADR-0093 P3, security review S2-6): a catalog id or a project-relative file. Both the
/// argument and the file the catalog returns must stay inside the project, have the expected type and exist.
/// </summary>
internal static class EditorRemoteAssets
{
    /// <summary>Resolves <paramref name="reference"/>; <paramref name="expectedType"/> null accepts any of the openable types.</summary>
    public static RemoteAssetReference Resolve(string projectDirectory, string reference, string expectedType)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, "No project is open.");
        }

        AssetInfo info;
        if (Guid.TryParse(reference, out Guid id))
        {
            info = AssetCatalog.Get(id);
        }
        else
        {
            string relative = reference.Replace('\\', '/');
            if (!EditorRemoteValidation.TryResolveInsideProject(projectDirectory, relative, out _, out string referenceLinkPath))
            {
                throw referenceLinkPath != null
                    ? EditorRemoteValidation.LinkRefusal(reference, referenceLinkPath)
                    : new EditorRemoteArgumentException($"'{reference}' is not a file inside the project.");
            }

            info = AssetCatalog.GetByFileName(relative) ?? AssetCatalog.GetByFileName(relative.Replace('/', '\\'));
        }

        if (info == null)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, $"'{reference}' is not in the asset catalog.");
        }

        // The catalog is project data: its file name is checked like an argument (S2-6).
        string catalogLinkPath = null;
        if (string.IsNullOrWhiteSpace(info.FileName)
            || !EditorRemoteValidation.TryResolveInsideProject(projectDirectory, info.FileName.Replace('\\', '/'), out string fullPath, out catalogLinkPath))
        {
            throw catalogLinkPath != null
                ? EditorRemoteValidation.LinkRefusal(info.FileName, catalogLinkPath)
                : new EditorRemoteArgumentException($"The catalog entry of '{reference}' points outside the project.");
        }

        string type = AssetInfo.InferAssetType(info.FileName);
        if (!string.Equals(type, info.AssetType, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(info.AssetType))
        {
            throw new EditorRemoteArgumentException($"The catalog entry of '{reference}' has type '{info.AssetType}' but file '{info.FileName}'.");
        }

        if (expectedType != null ? type != expectedType : !EditorRemoteAssetTypes.IsOpenable(type))
        {
            throw new EditorRemoteArgumentException(expectedType != null
                ? $"'{reference}' is a '{type}' asset, not a '{expectedType}'."
                : $"'{reference}' is a '{type}' asset: only material, particle, uiscreen, sound and entity assets open here (worlds use world.open).");
        }

        if (!File.Exists(fullPath))
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, $"The file of '{reference}' is missing: '{info.FileName}'.");
        }

        return new RemoteAssetReference(info, fullPath, type);
    }
}
