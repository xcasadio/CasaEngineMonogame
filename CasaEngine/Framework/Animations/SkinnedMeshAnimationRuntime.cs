using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class SkinnedMeshAnimationRuntime
{
    private readonly RiggedModel _riggedModel;
    private readonly List<AnimationClip> _animationClips = new();
    private int _currentAnimationIndex;

    public SkinnedMeshAnimationRuntime(RiggedModel riggedModel)
    {
        _riggedModel = riggedModel ?? throw new ArgumentNullException(nameof(riggedModel));
        SkeletonDefinition = riggedModel.SkeletonDefinition
            ?? throw new InvalidOperationException("The rigged model does not expose runtime skeleton data.");

        for (var clipIndex = 0; clipIndex < riggedModel.AnimationClips.Count; clipIndex++)
        {
            _animationClips.Add(riggedModel.AnimationClips[clipIndex]);
        }

        AnimationController = new AnimationController(SkeletonDefinition);
        AnimationController.AnimationEventTriggered += OnAnimationEventTriggered;
        ModelPose = new SkeletonPoseModel(SkeletonDefinition);
        SkinningPalette = new Matrix[riggedModel.GlobalShaderMatrixs.Length];

        ResetSkinningPaletteToIdentity();
        RefreshEvaluatedPose();
    }

    public RiggedModel RiggedModel => _riggedModel;

    public SkeletonDefinition SkeletonDefinition { get; }

    public AnimationController AnimationController { get; }

    public SkeletonPoseLocal LocalPose => AnimationController.OutputPose;

    public SkeletonPoseModel ModelPose { get; }

    public IReadOnlyList<AnimationClip> AnimationClips => _animationClips;

    public Matrix[] SkinningPalette { get; }

    public int CurrentPlayingAnimationIndex => _currentAnimationIndex;

    public int CurrentFrame { get; private set; }

    public bool AnimationRunning { get; private set; }

    public float CurrentAnimationFrameTime { get; private set; }

    public RootMotionMode RootMotionMode
    {
        get => AnimationController.RootMotionMode;
        set => AnimationController.RootMotionMode = value;
    }

    public event Action<AnimationEventKeyframe>? AnimationEventTriggered;

    public event Action<SkeletonPoseLocal, SkeletonPoseModel>? PosePostProcessing;

    public void Update(float elapsedTime)
    {
        AnimationController.Update(elapsedTime);
        RefreshEvaluatedPose();
    }

    public bool PlayAnimation(string animationName)
    {
        if (!TryGetAnimationIndex(animationName, out var animationIndex))
        {
            return false;
        }

        PlayAnimation(animationIndex);
        return true;
    }

    public void PlayAnimation(int animationIndex)
    {
        if (!TryResolveAnimationClip(animationIndex, out var resolvedAnimationIndex, out var clip))
        {
            return;
        }

        _currentAnimationIndex = resolvedAnimationIndex;
        AnimationController.Play(clip);
        RefreshEvaluatedPose();
    }

    public void PlayAnimationGraph(IAnimationGraphNode graphRoot)
    {
        AnimationController.PlayGraph(graphRoot);
        RefreshEvaluatedPose();
    }

    public void CrossFadeToAnimation(int animationIndex, float durationSeconds, AnimationCrossFadeSettings? settings = null)
    {
        if (!TryResolveAnimationClip(animationIndex, out var resolvedAnimationIndex, out var clip))
        {
            return;
        }

        _currentAnimationIndex = resolvedAnimationIndex;

        if (settings == null)
        {
            AnimationController.CrossFade(clip, durationSeconds, loop: true);
        }
        else
        {
            AnimationController.CrossFade(clip, durationSeconds, settings, loop: true);
        }

        RefreshEvaluatedPose();
    }

    public void StopAnimation()
    {
        AnimationController.Stop();
        RefreshEvaluatedPose();
    }

    public void PauseAnimation()
    {
        AnimationController.Pause();
        RefreshEvaluatedPose();
    }

    public void ResumeAnimation()
    {
        AnimationController.Resume();
        RefreshEvaluatedPose();
    }

    public void SeekAnimation(float timeSeconds)
    {
        AnimationController.Seek(timeSeconds);
        RefreshEvaluatedPose();
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
        AnimationController.SetLayerAnimation(layerIndex, clip, mask, weight, blendMode, loop, speed);
    }

    public void ClearAnimationLayer(int layerIndex)
    {
        AnimationController.ClearLayer(layerIndex);
    }

    public void SetAnimationLayerWeight(int layerIndex, float weight)
    {
        AnimationController.SetLayerWeight(layerIndex, weight);
    }

    public RootMotionDelta ConsumeRootMotionDelta()
    {
        return AnimationController.ConsumeRootMotionDelta();
    }

    public bool TryGetAnimationIndex(string animationName, out int animationIndex)
    {
        animationIndex = -1;
        if (string.IsNullOrWhiteSpace(animationName))
        {
            return false;
        }

        for (var clipIndex = 0; clipIndex < _animationClips.Count; clipIndex++)
        {
            if (string.Equals(_animationClips[clipIndex].Name, animationName, StringComparison.Ordinal))
            {
                animationIndex = clipIndex;
                return true;
            }
        }

        return false;
    }

    private void RefreshEvaluatedPose()
    {
        ModelPose.UpdateFromLocalPose(LocalPose);

        if (PosePostProcessing != null)
        {
            PosePostProcessing(LocalPose, ModelPose);
            ModelPose.UpdateFromLocalPose(LocalPose);
        }

        UpdateSkinningPalette();
        CurrentAnimationFrameTime = AnimationController.CurrentTimeSeconds;
        AnimationRunning = AnimationController.IsPlaying;
        UpdateCurrentFrame();
    }

    private void UpdateSkinningPalette()
    {
        for (var jointIndex = 0; jointIndex < SkeletonDefinition.Count; jointIndex++)
        {
            var paletteIndex = SkeletonDefinition.GetJoint(jointIndex).SkinPaletteIndex;
            if (paletteIndex < 0)
            {
                continue;
            }

            if ((uint)paletteIndex >= (uint)SkinningPalette.Length)
            {
                throw new InvalidOperationException($"Joint '{SkeletonDefinition.GetJoint(jointIndex).Name}' targets skin palette index {paletteIndex}, but the runtime palette only has {SkinningPalette.Length} slots.");
            }

            SkinningPalette[paletteIndex] = ModelPose.GetSkinningTransform(jointIndex);
        }
    }

    private void UpdateCurrentFrame()
    {
        if (AnimationController.HasAnimationGraph)
        {
            CurrentFrame = 0;
            return;
        }

        if (_currentAnimationIndex < 0 || _currentAnimationIndex >= _riggedModel.OriginalAnimations.Count)
        {
            CurrentFrame = 0;
            return;
        }

        var secondsPerFrame = (float)_riggedModel.OriginalAnimations[_currentAnimationIndex].SecondsPerFrame;
        CurrentFrame = secondsPerFrame > 0f
            ? (int)(CurrentAnimationFrameTime / secondsPerFrame)
            : 0;
    }

    private bool TryResolveAnimationClip(int requestedAnimationIndex, out int resolvedAnimationIndex, out AnimationClip clip)
    {
        resolvedAnimationIndex = -1;
        clip = null!;
        if (_animationClips.Count == 0)
        {
            return false;
        }

        resolvedAnimationIndex = requestedAnimationIndex;
        if (resolvedAnimationIndex < 0 || resolvedAnimationIndex >= _animationClips.Count)
        {
            resolvedAnimationIndex = 0;
        }

        clip = _animationClips[resolvedAnimationIndex];
        return true;
    }

    private void ResetSkinningPaletteToIdentity()
    {
        for (var paletteIndex = 0; paletteIndex < SkinningPalette.Length; paletteIndex++)
        {
            SkinningPalette[paletteIndex] = Matrix.Identity;
        }
    }

    private void OnAnimationEventTriggered(AnimationEventKeyframe eventKeyframe)
    {
        AnimationEventTriggered?.Invoke(eventKeyframe);
    }
}