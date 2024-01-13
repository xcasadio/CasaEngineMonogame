using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Skinned Mesh")]
public class SkinnedMeshComponent : Component, IBoundingBoxable
{
    public override int ComponentId => (int)ComponentIds.SkinnedMesh;

    private SkinnedMeshRendererComponent? _skinnedMeshRendererComponent;
    private RiggedModel? _skinnedMesh;

    public RiggedModel? SkinnedMesh
    {
        get { return _skinnedMesh; }
        set
        {
            _skinnedMesh = value;
#if EDITOR
            OnPropertyChanged();
#endif
        }
    }

    public override void Initialize(Entity entity)
    {
        base.Initialize(entity);

        _skinnedMeshRendererComponent = Owner.Game.GetGameComponent<SkinnedMeshRendererComponent>();
        //Mesh?.Initialize(game.GraphicsDevice);
        //Mesh?.Texture?.Initialize(game.GraphicsDevice, game.GameManager.AssetContentManager);
    }

    public override void Update(float elapsedTime)
    {
        if (SkinnedMesh == null)
        {
            return;
        }

        SkinnedMesh.Update(elapsedTime);
    }

    public override void Draw()
    {
        if (SkinnedMesh == null)
        {
            return;
        }

        var camera = Owner.Game.GameManager.ActiveCamera;
        _skinnedMeshRendererComponent.AddMesh(
            SkinnedMesh,
            Owner.Coordinates.WorldMatrix,
            camera.ViewMatrix,
            camera.ProjectionMatrix,
            camera.Position);
    }

    public BoundingBox BoundingBox
    {
        get
        {
            var min = Vector3.One * int.MaxValue;
            var max = Vector3.One * int.MinValue;

            if (SkinnedMesh != null)
            {
                foreach (var mesh in SkinnedMesh.Meshes)
                {
                    min = Vector3.Min(min, mesh.Min);
                    max = Vector3.Max(max, mesh.Max);
                }
            }
            else // default box
            {
                const float length = 0.5f;
                min = Vector3.One * -length;
                max = Vector3.One * length;
            }

            min = Vector3.Transform(min, Owner.Coordinates.WorldMatrix);
            max = Vector3.Transform(max, Owner.Coordinates.WorldMatrix);

            return new BoundingBox(min, max);
        }
    }

    public override Component Clone()
    {
        var component = new SkinnedMeshComponent();

        component._skinnedMesh = _skinnedMesh;

        return component;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        var meshElement = element.GetProperty("skinned_mesh");

        if (meshElement.ToString() != "null")
        {
            //_mesh = new SkinModel();
            //_mesh.Load(meshElement);
        }
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        JObject newJObject = new();
        //if (SkinnedMesh != null)
        //{
        //    SkinnedMesh.Save(newJObject);
        //    jObject.Add("skinned_mesh", newJObject);
        //}
        //else
        //{
        //    jObject.Add("mesh", "null");
        //}
    }
#endif
}
