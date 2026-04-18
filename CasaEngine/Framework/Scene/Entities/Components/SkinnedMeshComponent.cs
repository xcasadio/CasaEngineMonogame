using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Skinned Mesh")]
public class SkinnedMeshComponent : PrimitiveComponent
{
    private SkinnedMeshRendererComponent? _skinnedMeshRendererComponent;
    private RiggedModel? _boundRiggedModel;
    private SkinnedMesh? _skinnedMesh;

    public Guid SkinnedMeshAssetId { get; set; } = Guid.Empty;
    public SkinnedMesh? SkinnedMesh
    {
        get => _skinnedMesh;
        set
        {
            if (ReferenceEquals(_skinnedMesh, value))
            {
                return;
            }

            _skinnedMesh = value;
            BindRiggedModelEvents();
        }
    }

    public RootMotionMode RootMotionMode
    {
        get => SkinnedMesh?.RiggedModel?.AnimationController?.RootMotionMode ?? RootMotionMode.Observe;
        set
        {
            var controller = SkinnedMesh?.RiggedModel?.AnimationController;
            if (controller != null)
            {
                controller.RootMotionMode = value;
            }
        }
    }

    public event Action<AnimationEventKeyframe>? AnimationEventTriggered;

    public SkinnedMeshComponent()
    {

    }

    public SkinnedMeshComponent(SkinnedMeshComponent other) : base(other)
    {
        SkinnedMesh = other.SkinnedMesh;
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        _skinnedMeshRendererComponent = Owner.World.Game.GetGameComponent<SkinnedMeshRendererComponent>();

        if (SkinnedMeshAssetId != Guid.Empty)
        {
            SkinnedMesh = world.Game.AssetContentManager.Load<SkinnedMesh>(SkinnedMeshAssetId);
            SkinnedMesh?.Initialize(Owner.World.Game.AssetContentManager);
            BindRiggedModelEvents();
        }
    }

    public override SkinnedMeshComponent Clone()
    {
        return new SkinnedMeshComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        SkinnedMesh?.RiggedModel?.Update(elapsedTime);

        base.Update(elapsedTime);
    }

    public override void Draw(float elapsedTime)
    {
        if (SkinnedMesh?.RiggedModel == null || _skinnedMeshRendererComponent == null)
        {
            return;
        }

        _skinnedMeshRendererComponent.AddMesh(
            SkinnedMesh.RiggedModel,
            WorldMatrixWithScale);
    }

    public override BoundingBox GetBoundingBox()
    {
        if (SkinnedMesh?.RiggedModel != null)
        {
            bool hasBounds = false;
            BoundingBox bounds = default;

            foreach (var mesh in SkinnedMesh.RiggedModel.Meshes)
            {
                var meshWorld = WorldMatrixWithScale * SkinnedMesh.RiggedModel.GetMeshNodeTransform(mesh);
                var meshBounds = new BoundingBox(mesh.Min, mesh.Max).Transform(meshWorld);

                if (!hasBounds)
                {
                    bounds = meshBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.ExpandBy(meshBounds);
                }
            }

            if (hasBounds)
            {
                return bounds;
            }
        }

        return base.GetBoundingBox();
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element.ContainsKey("skinned_mesh_id"))
        {
            SkinnedMeshAssetId = element["skinned_mesh_id"].GetGuid();
        }
    }

    public bool PlayAnimation(string animationName)
    {
        return SkinnedMesh?.RiggedModel?.BeginAnimation(animationName) == true;
    }

    public void PlayAnimation(int animationIndex)
    {
        SkinnedMesh?.RiggedModel?.BeginAnimation(animationIndex);
    }

    public void CrossFadeToAnimation(int animationIndex, float durationSeconds)
    {
        SkinnedMesh?.RiggedModel?.CrossFadeToAnimation(animationIndex, durationSeconds);
    }

    public void PlayAnimationGraph(IAnimationGraphNode graphRoot)
    {
        SkinnedMesh?.RiggedModel?.PlayAnimationGraph(graphRoot);
    }

    public void StopAnimation()
    {
        SkinnedMesh?.RiggedModel?.StopAnimation();
    }

    public void PauseAnimation()
    {
        SkinnedMesh?.RiggedModel?.PauseAnimation();
    }

    public void ResumeAnimation()
    {
        SkinnedMesh?.RiggedModel?.ResumeAnimation();
    }

    public void SeekAnimation(float timeSeconds)
    {
        SkinnedMesh?.RiggedModel?.SeekAnimation(timeSeconds);
    }

    public void SetAnimationLayer(
        int layerIndex,
        AnimationClip clip,
        BoneMask? mask = null,
        float weight = 1f,
        AnimationLayerBlendMode blendMode = AnimationLayerBlendMode.Override,
        bool loop = true,
        float speed = 1f)
    {
        SkinnedMesh?.RiggedModel?.AnimationController?.SetLayerAnimation(layerIndex, clip, mask, weight, blendMode, loop, speed);
    }

    public void ClearAnimationLayer(int layerIndex)
    {
        SkinnedMesh?.RiggedModel?.AnimationController?.ClearLayer(layerIndex);
    }

    public void SetAnimationLayerWeight(int layerIndex, float weight)
    {
        SkinnedMesh?.RiggedModel?.AnimationController?.SetLayerWeight(layerIndex, weight);
    }

    public RootMotionDelta ConsumeRootMotionDelta()
    {
        return SkinnedMesh?.RiggedModel?.AnimationController?.ConsumeRootMotionDelta() ?? RootMotionDelta.Identity;
    }

    private void BindRiggedModelEvents()
    {
        var riggedModel = SkinnedMesh?.RiggedModel;
        if (ReferenceEquals(_boundRiggedModel, riggedModel))
        {
            return;
        }

        if (_boundRiggedModel != null)
        {
            _boundRiggedModel.AnimationEventTriggered -= OnRiggedModelAnimationEventTriggered;
            _boundRiggedModel = null;
        }

        if (riggedModel == null)
        {
            return;
        }

        _boundRiggedModel = riggedModel;
        _boundRiggedModel.AnimationEventTriggered += OnRiggedModelAnimationEventTriggered;
    }

    private void OnRiggedModelAnimationEventTriggered(AnimationEventKeyframe eventKeyframe)
    {
        AnimationEventTriggered?.Invoke(eventKeyframe);
    }

}
