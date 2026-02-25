using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

/// <summary>
/// Renders a <see cref="StaticModel"/> asset in the world.
/// The model is loaded once during <see cref="InitializeWithWorld"/> and its
/// full geometry hierarchy is submitted every frame to
/// <see cref="StaticMeshRendererComponent"/>.
/// </summary>
[DisplayName("Static Model")]
public class StaticModelComponent : PrimitiveComponent
{
    private StaticMeshRendererComponent? _meshRendererComponent;

    // ------------------------------------------------------------------
    //  Public properties
    // ------------------------------------------------------------------

    /// <summary>Asset ID of the <see cref="StaticModel"/> to render.</summary>
    public Guid StaticModelAssetId { get; set; } = Guid.Empty;

    /// <summary>Runtime reference to the loaded model.</summary>
    public Graphics.StaticModel? StaticModel { get; private set; }

    // ------------------------------------------------------------------
    //  Constructors / Clone
    // ------------------------------------------------------------------

    public StaticModelComponent() { }

    public StaticModelComponent(StaticModelComponent other) : base(other)
    {
        StaticModelAssetId = other.StaticModelAssetId;
    }

    public override StaticModelComponent Clone() => new StaticModelComponent(this);

    // ------------------------------------------------------------------
    //  Lifecycle
    // ------------------------------------------------------------------

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        _meshRendererComponent = world.Game.GetGameComponent<StaticMeshRendererComponent>()!;

        if (StaticModelAssetId != Guid.Empty && StaticModel == null)
        {
            StaticModel = world.Game.AssetContentManager.Load<Graphics.StaticModel>(StaticModelAssetId);
            StaticModel?.Initialize(world.Game.AssetContentManager);
        }
    }

    // ------------------------------------------------------------------
    //  Rendering
    // ------------------------------------------------------------------

    public override void Draw(float elapsedTime)
    {
        base.Draw(elapsedTime);

        if (StaticModel?.RootNode == null || _meshRendererComponent == null)
        {
            return;
        }

        DrawNode(StaticModel.RootNode, WorldMatrixWithScale);
    }

    private void DrawNode(StaticModelNode node, Matrix parentWorldMatrix)
    {
        var nodeWorld = node.LocalTransform * parentWorldMatrix;

        if (node.MeshIndex >= 0 && node.MeshIndex < StaticModel!.Meshes.Count)
        {
            var mesh = StaticModel.Meshes[node.MeshIndex];
            if (mesh.VertexBuffer != null)
            {
                var invTranspose = Matrix.Transpose(Matrix.Invert(nodeWorld));
                _meshRendererComponent!.AddMesh(mesh, nodeWorld, invTranspose);
            }
        }

        foreach (var child in node.Children)
        {
            DrawNode(child, nodeWorld);
        }
    }

    // ------------------------------------------------------------------
    //  Bounding box
    // ------------------------------------------------------------------

    public override BoundingBox GetBoundingBox()
    {
        if (StaticModel?.RootNode == null)
        {
            return base.GetBoundingBox();
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        bool any = false;

        AccumulateBounds(StaticModel.RootNode, WorldMatrixWithScale, ref min, ref max, ref any);

        return any ? new BoundingBox(min, max) : base.GetBoundingBox();
    }

    private void AccumulateBounds(StaticModelNode node, Matrix parentWorld, ref Vector3 min, ref Vector3 max, ref bool any)
    {
        var nodeWorld = node.LocalTransform * parentWorld;

        if (node.MeshIndex >= 0 && node.MeshIndex < StaticModel!.Meshes.Count)
        {
            var mesh = StaticModel.Meshes[node.MeshIndex];
            foreach (var v in mesh.GetVertices())
            {
                var p = Vector3.Transform(v.Position, nodeWorld);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
                any = true;
            }
        }

        foreach (var child in node.Children)
        {
            AccumulateBounds(child, nodeWorld, ref min, ref max, ref any);
        }
    }

    // ------------------------------------------------------------------
    //  Serialization
    // ------------------------------------------------------------------

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element.ContainsKey("static_model_asset_id"))
        {
            StaticModelAssetId = element["static_model_asset_id"]!.GetGuid();
        }
    }

#if EDITOR
    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("static_model_asset_id", StaticModelAssetId.ToString());
    }
#endif
}
