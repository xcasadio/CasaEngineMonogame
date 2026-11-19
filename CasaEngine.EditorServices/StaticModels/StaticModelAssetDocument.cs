using System.Globalization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering.Models;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.StaticModels;

/// <summary>
/// The model behind the <c>.staticModel</c> document of the editor (plan P8): a <see cref="StaticModel"/> read from its file and
/// the edits of the material of its meshes and sub-meshes. The edits live here, not in the panel, so they are tested without a
/// window and the automation option uses the same code as the controls.
/// </summary>
/// <remarks>
/// Only <c>material_asset_id</c> changes: the geometry (vertices, indices), the node tree and every other field stay as the
/// loader read them, and the serializer writes them back.
/// </remarks>
public sealed class StaticModelAssetDocument
{
    /// <summary>Asset type of the catalogued assets a mesh can take as its material.</summary>
    public const string MaterialAssetType = "material";

    /// <summary>Index that designates the mesh itself rather than one of its sub-meshes.</summary>
    public const int WholeMesh = -1;

    private StaticModelAssetDocument(StaticModel model)
    {
        Model = model;
    }

    /// <summary>The model as the serializer writes it back.</summary>
    public StaticModel Model { get; }

    public static bool TryLoad(string fullPath, out StaticModelAssetDocument document, out string error)
    {
        document = null;
        error = null;

        try
        {
            var model = new StaticModel();
            model.Load(JObject.Parse(File.ReadAllText(fullPath)));
            model.FileName = fullPath;
            document = new StaticModelAssetDocument(model);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException or NullReferenceException or FormatException or ArgumentException or InvalidCastException)
        {
            error = $"Cannot read static model '{fullPath}': {exception.Message}";
            return false;
        }
    }

    /// <summary>The material asset id of a mesh (<paramref name="subMeshIndex"/> = <see cref="WholeMesh"/>) or of a sub-mesh; empty means none.</summary>
    public Guid GetMaterialAssetId(int meshIndex, int subMeshIndex = WholeMesh)
    {
        if (!TryGetTarget(meshIndex, subMeshIndex, out StaticModelMesh mesh, out SubMesh subMesh, out _))
        {
            return Guid.Empty;
        }

        return subMesh?.MaterialAssetId ?? mesh.MaterialAssetId;
    }

    /// <summary>
    /// Sets the material of a mesh or of one of its sub-meshes; <see cref="Guid.Empty"/> removes it. The resolved runtime
    /// material of what changed is dropped so that the next resolution reads the new id. On failure nothing changes and
    /// <paramref name="error"/> says why. The id is not looked up in the catalog here, see <see cref="TryParseMaterialId"/>.
    /// </summary>
    public bool TrySetMaterial(int meshIndex, int subMeshIndex, Guid materialAssetId, out string error)
    {
        if (!TryGetTarget(meshIndex, subMeshIndex, out StaticModelMesh mesh, out SubMesh subMesh, out error))
        {
            return false;
        }

        if (subMesh != null)
        {
            subMesh.MaterialAssetId = materialAssetId;
            subMesh.Material = null;
            return true;
        }

        mesh.MaterialAssetId = materialAssetId;
        mesh.Material = null;

        // A sub-mesh without a material of its own shows the one of its mesh: its resolved copy is stale too.
        for (int i = 0; i < mesh.SubMeshes.Count; i++)
        {
            if (mesh.SubMeshes[i].MaterialAssetId == Guid.Empty)
            {
                mesh.SubMeshes[i].Material = null;
            }
        }

        return true;
    }

    /// <summary>Reads a mesh index from automation text. Fails with a message when it is not a whole number or when no mesh has that index.</summary>
    public bool TryParseMeshIndex(string text, out int meshIndex, out string error)
    {
        error = null;
        if (!int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out meshIndex))
        {
            error = $"'{text}' is not a mesh index.";
            return false;
        }

        if (meshIndex < 0 || meshIndex >= Model.Meshes.Count)
        {
            error = $"mesh index {meshIndex} is out of range (the model has {Model.Meshes.Count} mesh(es)).";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads a material id from text and checks it names a catalogued <c>material</c> asset. An empty id is refused: pass
    /// <see cref="Guid.Empty"/> to <see cref="TrySetMaterial"/> to remove a material.
    /// </summary>
    /// <param name="lookup">Finds a catalogued asset by id; the asset catalog of the project when null.</param>
    public static bool TryParseMaterialId(string text, Func<Guid, AssetInfo> lookup, out Guid materialAssetId, out string error)
    {
        error = null;
        if (!Guid.TryParse(text?.Trim(), out materialAssetId) || materialAssetId == Guid.Empty)
        {
            materialAssetId = Guid.Empty;
            error = $"'{text}' is not a material asset id.";
            return false;
        }

        AssetInfo info = (lookup ?? AssetCatalog.Get)(materialAssetId);
        if (info == null)
        {
            error = $"{materialAssetId} is not in the asset catalog.";
            materialAssetId = Guid.Empty;
            return false;
        }

        if (!string.Equals(info.AssetType, MaterialAssetType, StringComparison.OrdinalIgnoreCase))
        {
            error = $"'{info.Name}' ({materialAssetId}) is a '{info.AssetType}' asset, not a material.";
            materialAssetId = Guid.Empty;
            return false;
        }

        return true;
    }

    /// <summary>Reads <c>material_asset_id</c> of a mesh from the file on disk, for checking what a save really wrote. Empty when the mesh names none.</summary>
    public static bool TryReadMeshMaterialId(string fullPath, int meshIndex, out Guid materialAssetId)
    {
        materialAssetId = Guid.Empty;
        try
        {
            if (JObject.Parse(File.ReadAllText(fullPath))["meshes"] is not JArray meshes
                || meshIndex < 0
                || meshIndex >= meshes.Count)
            {
                return false;
            }

            string text = meshes[meshIndex]["material_asset_id"]?.Value<string>();
            if (!string.IsNullOrEmpty(text))
            {
                materialAssetId = Guid.Parse(text);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException or FormatException or InvalidCastException)
        {
            return false;
        }
    }

    private bool TryGetTarget(int meshIndex, int subMeshIndex, out StaticModelMesh mesh, out SubMesh subMesh, out string error)
    {
        mesh = null;
        subMesh = null;
        error = null;

        if (meshIndex < 0 || meshIndex >= Model.Meshes.Count)
        {
            error = $"mesh index {meshIndex} is out of range (the model has {Model.Meshes.Count} mesh(es)).";
            return false;
        }

        mesh = Model.Meshes[meshIndex];
        if (subMeshIndex == WholeMesh)
        {
            return true;
        }

        if (subMeshIndex < 0 || subMeshIndex >= mesh.SubMeshes.Count)
        {
            error = $"sub-mesh index {subMeshIndex} is out of range (mesh {meshIndex} has {mesh.SubMeshes.Count}).";
            return false;
        }

        subMesh = mesh.SubMeshes[subMeshIndex];
        return true;
    }
}
