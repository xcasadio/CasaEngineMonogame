using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Skinned Mesh")]
public class SkinnedMeshComponent : PrimitiveComponent
{
    private SkinnedMeshRendererComponent? _skinnedMeshRendererComponent;
    private SkinnedMesh? _skinnedMesh;
    private SkinnedMeshAnimationRuntime? _animationRuntime;
    private RiggedModelPoseProvider? _legacyPoseProvider;
    private readonly List<TwoBoneIkConstraint> _twoBoneIkConstraints = new();
    private RootMotionMode _rootMotionMode = RootMotionMode.Observe;
    private SkinningModeSelection _skinningModeSelection = SkinningModeSelection.RiggedModelDefault;

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
            EnsureAnimationRuntime();
        }
    }

    public RootMotionMode RootMotionMode
    {
        get => _animationRuntime?.RootMotionMode ?? _rootMotionMode;
        set
        {
            _rootMotionMode = value;

            if (_animationRuntime != null)
            {
                _animationRuntime.RootMotionMode = value;
            }
        }
    }

    [DefaultValue(SkinningModeSelection.RiggedModelDefault)]
    public SkinningModeSelection SkinningModeSelection
    {
        get => _skinningModeSelection;
        set => _skinningModeSelection = value;
    }

    public SkeletonDefinition? SkeletonDefinition => _animationRuntime?.SkeletonDefinition ?? SkinnedMesh?.RiggedModel?.SkeletonDefinition;

    public SkeletonPoseModel? CurrentModelPose => _animationRuntime?.ModelPose;

    public IReadOnlyList<AnimationClip> AnimationClips => _animationRuntime?.AnimationClips ?? SkinnedMesh?.RiggedModel?.AnimationClips ?? Array.Empty<AnimationClip>();

    public IReadOnlyList<MorphTarget> MorphTargets => _animationRuntime?.MorphTargets ?? SkinnedMesh?.RiggedModel?.MorphTargets ?? Array.Empty<MorphTarget>();

    public IReadOnlyList<MorphClip> MorphClips => _animationRuntime?.MorphClips ?? SkinnedMesh?.RiggedModel?.MorphClips ?? Array.Empty<MorphClip>();

    public IReadOnlyList<TwoBoneIkConstraint> TwoBoneIkConstraints => _twoBoneIkConstraints;

    public event Action<AnimationEventKeyframe>? AnimationEventTriggered;

    public SkinnedMeshComponent()
    {

    }

    public SkinnedMeshComponent(SkinnedMeshComponent other) : base(other)
    {
        _rootMotionMode = other._rootMotionMode;
        _skinningModeSelection = other._skinningModeSelection;

        for (var constraintIndex = 0; constraintIndex < other._twoBoneIkConstraints.Count; constraintIndex++)
        {
            _twoBoneIkConstraints.Add(other._twoBoneIkConstraints[constraintIndex]);
        }

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
        }

        EnsureAnimationRuntime();
    }

    public override SkinnedMeshComponent Clone()
    {
        return new SkinnedMeshComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        EnsureAnimationRuntime();
        _animationRuntime?.Update(elapsedTime);

        base.Update(elapsedTime);
    }

    public override void Draw(float elapsedTime)
    {
        if (SkinnedMesh?.RiggedModel == null || _skinnedMeshRendererComponent == null)
        {
            return;
        }

        var poseProvider = GetPoseProvider(SkinnedMesh.RiggedModel);
        if (poseProvider == null)
        {
            return;
        }

        _skinnedMeshRendererComponent.AddMesh(
            SkinnedMesh.RiggedModel,
            WorldMatrixWithScale,
            poseProvider,
            SkinningModeSelection);
    }

    public override BoundingBox GetBoundingBox()
    {
        if (SkinnedMesh?.RiggedModel != null)
        {
            bool hasBounds = false;
            BoundingBox bounds = default;
            var poseProvider = GetPoseProvider(SkinnedMesh.RiggedModel);

            foreach (var mesh in SkinnedMesh.RiggedModel.Meshes)
            {
                var meshNodeTransform = poseProvider?.GetMeshNodeTransform(mesh) ?? SkinnedMesh.RiggedModel.GetMeshNodeTransform(mesh);
                var meshWorld = WorldMatrixWithScale * meshNodeTransform;
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

        if (element.ContainsKey("skinning_mode_selection"))
        {
            var skinningModeSelection = element["skinning_mode_selection"];
            if (skinningModeSelection != null)
            {
                if (skinningModeSelection.Type == JTokenType.Integer)
                {
                    var selectionValue = skinningModeSelection.Value<int>();
                    if (Enum.IsDefined(typeof(SkinningModeSelection), selectionValue))
                    {
                        SkinningModeSelection = (SkinningModeSelection)selectionValue;
                    }
                }
                else if (Enum.TryParse<SkinningModeSelection>(skinningModeSelection.ToString(), true, out var parsedSelection))
                {
                    SkinningModeSelection = parsedSelection;
                }
            }
        }
    }

    public bool PlayAnimation(string animationName)
    {
        EnsureAnimationRuntime();
        return _animationRuntime?.PlayAnimation(animationName) == true;
    }

    public void PlayAnimation(int animationIndex)
    {
        EnsureAnimationRuntime();
        _animationRuntime?.PlayAnimation(animationIndex);
    }

    public void CrossFadeToAnimation(int animationIndex, float durationSeconds)
    {
        EnsureAnimationRuntime();
        _animationRuntime?.CrossFadeToAnimation(animationIndex, durationSeconds);
    }

    public void CrossFadeToAnimation(int animationIndex, float durationSeconds, AnimationCrossFadeSettings? settings)
    {
        EnsureAnimationRuntime();
        _animationRuntime?.CrossFadeToAnimation(animationIndex, durationSeconds, settings);
    }

    public void PlayAnimationGraph(IAnimationGraphNode graphRoot)
    {
        EnsureAnimationRuntime();
        _animationRuntime?.PlayAnimationGraph(graphRoot);
    }

    public void StopAnimation()
    {
        EnsureAnimationRuntime();
        _animationRuntime?.StopAnimation();
    }

    public void PauseAnimation()
    {
        EnsureAnimationRuntime();
        _animationRuntime?.PauseAnimation();
    }

    public void ResumeAnimation()
    {
        EnsureAnimationRuntime();
        _animationRuntime?.ResumeAnimation();
    }

    public void SeekAnimation(float timeSeconds)
    {
        EnsureAnimationRuntime();
        _animationRuntime?.SeekAnimation(timeSeconds);
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
        EnsureAnimationRuntime();
        _animationRuntime?.SetAnimationLayer(layerIndex, clip, mask, weight, blendMode, loop, speed);
    }

    public void ClearAnimationLayer(int layerIndex)
    {
        EnsureAnimationRuntime();
        _animationRuntime?.ClearAnimationLayer(layerIndex);
    }

    public void SetAnimationLayerWeight(int layerIndex, float weight)
    {
        EnsureAnimationRuntime();
        _animationRuntime?.SetAnimationLayerWeight(layerIndex, weight);
    }

    public void SetTwoBoneIkConstraint(int constraintIndex, TwoBoneIkConstraint constraint)
    {
        if (constraintIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(constraintIndex));
        }

        while (_twoBoneIkConstraints.Count <= constraintIndex)
        {
            _twoBoneIkConstraints.Add(default);
        }

        _twoBoneIkConstraints[constraintIndex] = constraint;
    }

    public void ClearTwoBoneIkConstraint(int constraintIndex)
    {
        if (constraintIndex < 0 || constraintIndex >= _twoBoneIkConstraints.Count)
        {
            return;
        }

        _twoBoneIkConstraints[constraintIndex] = default;
    }

    public void ClearTwoBoneIkConstraints()
    {
        _twoBoneIkConstraints.Clear();
    }

    public RootMotionDelta ConsumeRootMotionDelta()
    {
        EnsureAnimationRuntime();
        return _animationRuntime?.ConsumeRootMotionDelta() ?? RootMotionDelta.Identity;
    }

    private void EnsureAnimationRuntime()
    {
        var riggedModel = SkinnedMesh?.RiggedModel;
        if (riggedModel == null || riggedModel.SkeletonDefinition == null)
        {
            ReleaseAnimationRuntime();
            return;
        }

        if (_animationRuntime != null
            && ReferenceEquals(_animationRuntime.RiggedModel, riggedModel)
            && ReferenceEquals(_animationRuntime.SkeletonDefinition, riggedModel.SkeletonDefinition)
            && _animationRuntime.AnimationClips.Count == riggedModel.AnimationClips.Count)
        {
            return;
        }

        ReleaseAnimationRuntime();

        _animationRuntime = new SkinnedMeshAnimationRuntime(riggedModel)
        {
            RootMotionMode = _rootMotionMode,
        };
        _animationRuntime.AnimationEventTriggered += OnAnimationRuntimeAnimationEventTriggered;
        _animationRuntime.PosePostProcessing += OnAnimationRuntimePosePostProcessing;
    }

    private void ReleaseAnimationRuntime()
    {
        if (_animationRuntime == null)
        {
            return;
        }

        _animationRuntime.AnimationEventTriggered -= OnAnimationRuntimeAnimationEventTriggered;
        _animationRuntime.PosePostProcessing -= OnAnimationRuntimePosePostProcessing;
        _animationRuntime = null;
    }

    private ISkinnedMeshPoseProvider? GetPoseProvider(RiggedModel riggedModel)
    {
        if (_animationRuntime != null && ReferenceEquals(_animationRuntime.RiggedModel, riggedModel))
        {
            return _animationRuntime;
        }

        if (_legacyPoseProvider == null || !ReferenceEquals(_legacyPoseProvider.RiggedModel, riggedModel))
        {
            _legacyPoseProvider = new RiggedModelPoseProvider(riggedModel);
        }

        return _legacyPoseProvider;
    }

    private void OnAnimationRuntimeAnimationEventTriggered(AnimationEventKeyframe eventKeyframe)
    {
        AnimationEventTriggered?.Invoke(eventKeyframe);
    }

    private void OnAnimationRuntimePosePostProcessing(SkeletonPoseLocal localPose, SkeletonPoseModel modelPose)
    {
        for (var constraintIndex = 0; constraintIndex < _twoBoneIkConstraints.Count; constraintIndex++)
        {
            var constraint = _twoBoneIkConstraints[constraintIndex];
            if (!constraint.Enabled || constraint.Weight <= 0f)
            {
                continue;
            }

            IkSolverTwoBone.Solve(localPose, modelPose, constraint);
        }
    }
}
