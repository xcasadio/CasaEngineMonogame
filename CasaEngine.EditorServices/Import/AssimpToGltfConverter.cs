using Assimp;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;

namespace CasaEngine.EditorServices.Import;

/// <summary>
/// Editor-only utility that converts a non-glTF 3-D file (FBX, OBJ, COLLADA, .X, …) into a
/// self-contained binary glTF (<c>.glb</c>). The engine runtime only loads glTF/GLB (via
/// SharpGLTF), so every other source format is normalised to <c>.glb</c> first and then read
/// back by the shared SharpGLTF readers.
/// <para/>
/// The conversion is two-stage: AssimpNetter exports an intermediate binary glTF (which may
/// reference textures by external URI), then SharpGLTF re-reads it with a resolver that pulls
/// those textures from the source asset's directory by file name and embeds them, producing a
/// fully self-contained <c>.glb</c>.
/// </summary>
public static class AssimpToGltfConverter
{
    // Assimp's binary glTF 2.0 exporter id.
    private const string GlbExportFormatId = "glb2";

    private static readonly PostProcessSteps ConversionPostProcess =
        PostProcessSteps.Triangulate
        | PostProcessSteps.LimitBoneWeights
        | PostProcessSteps.JoinIdenticalVertices
        | PostProcessSteps.GenerateSmoothNormals;

    /// <summary>
    /// True when <paramref name="fileName"/> is a non-glTF format that must be converted before import.
    /// </summary>
    public static bool RequiresConversion(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        if (extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".glb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        using var context = new AssimpContext();
        return context.IsImportFormatSupported(extension);
    }

    /// <summary>
    /// Imports <paramref name="sourceFilePath"/> with AssimpNetter and writes a self-contained binary
    /// <c>.glb</c> at <paramref name="destinationGlbPath"/>. Returns the destination path on success.
    /// </summary>
    public static string Convert(string sourceFilePath, string destinationGlbPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationGlbPath);

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Source model file not found.", sourceFilePath);
        }

        var destinationDirectory = Path.GetDirectoryName(destinationGlbPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        string sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath)) ?? string.Empty;
        string intermediateDirectory = Path.Combine(Path.GetTempPath(), "CasaEngineGlbConvert", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(intermediateDirectory);
        string intermediateGlb = Path.Combine(intermediateDirectory, "intermediate.glb");

        try
        {
            using (var context = new AssimpContext())
            {
                if (!context.GetSupportedExportFormats().Any(format =>
                        string.Equals(format.FormatId, GlbExportFormatId, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new NotSupportedException($"AssimpNetter does not expose the '{GlbExportFormatId}' export format.");
                }

                var scene = context.ImportFile(sourceFilePath, ConversionPostProcess);
                if (!context.ExportFile(scene, intermediateGlb, GlbExportFormatId))
                {
                    throw new InvalidOperationException($"AssimpNetter failed to export '{sourceFilePath}' to glb.");
                }
            }

            // Re-read with a resolver that embeds external textures (resolved by file name from the
            // source directory) so the final glb is fully self-contained. Validation is skipped
            // because Assimp's exporter emits inverse-bind matrices that fail strict glTF validation
            // yet are correct for linear-blend skinning.
            var readContext = ReadContext.Create(assetName => ResolveAsset(assetName, intermediateDirectory, sourceDirectory));
            readContext.Validation = ValidationMode.Skip;
            var model = readContext.ReadSchema2(Path.GetFileName(intermediateGlb));
            model.SaveGLB(destinationGlbPath, new WriteSettings { Validation = ValidationMode.Skip });
            return destinationGlbPath;
        }
        finally
        {
            TryDeleteDirectory(intermediateDirectory);
        }
    }

    /// <summary>
    /// Returns a glTF/GLB path for <paramref name="sourceFilePath"/>: the file itself when it is
    /// already glTF/GLB, otherwise a converted sibling <c>.glb</c> created next to it.
    /// </summary>
    public static string EnsureGltf(string sourceFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        string extension = Path.GetExtension(sourceFilePath);
        if (extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".glb", StringComparison.OrdinalIgnoreCase))
        {
            return sourceFilePath;
        }

        string destinationGlbPath = Path.ChangeExtension(sourceFilePath, ".glb");
        return Convert(sourceFilePath, destinationGlbPath);
    }

    private static ArraySegment<byte> ResolveAsset(string assetName, string intermediateDirectory, string sourceDirectory)
    {
        // The glb container and any sibling files Assimp wrote next to it.
        string directPath = Path.Combine(intermediateDirectory, assetName);
        if (File.Exists(directPath))
        {
            return new ArraySegment<byte>(File.ReadAllBytes(directPath));
        }

        // External textures: Assimp may reference them with an original sub-path (e.g. "kid/Tex.png")
        // that does not exist on disk; resolve by file name from the source asset's directory.
        if (!string.IsNullOrWhiteSpace(sourceDirectory))
        {
            string byNamePath = Path.Combine(sourceDirectory, Path.GetFileName(assetName));
            if (File.Exists(byNamePath))
            {
                return new ArraySegment<byte>(File.ReadAllBytes(byNamePath));
            }
        }

        return new ArraySegment<byte>(Array.Empty<byte>());
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of the temporary conversion artifacts.
        }
    }
}
