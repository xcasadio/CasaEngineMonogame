namespace CasaEngine.EditorServices;

/// <summary>The material definition the import writes for the materials of an imported static model (ADR-0109).</summary>
public enum ImportedMaterialTemplate
{
    /// <summary>The Blinn-Phong <c>lit-diffuse</c> definition. The default, and what the editor imports.</summary>
    LitDiffuse,

    /// <summary>
    /// The metallic-roughness <c>lit-pbr</c> definition, written from the glTF material channels: factors, the five textures, the alpha
    /// mode and the double-sided flag. glTF colour factors are encoded to display colours.
    /// </summary>
    LitPbr,
}

/// <summary>Options of <see cref="EditorAssetImportService.ImportFile(string, string, ILegacyMaterialImportProfile?, EditorAssetImportOptions)"/>.</summary>
public sealed class EditorAssetImportOptions
{
    /// <summary>The material definition written for the materials of an imported static model. Defaults to <see cref="ImportedMaterialTemplate.LitDiffuse"/>.</summary>
    public ImportedMaterialTemplate MaterialTemplate { get; init; } = ImportedMaterialTemplate.LitDiffuse;
}
