using CasaEngine.Core.Math;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Animations;

public sealed class SkinnedMeshAnimationRuntime : ISkinnedMeshPoseProvider, IDisposable
{
    private const float MorphWeightEpsilon = 0.0001f;

    private readonly RiggedModel _riggedModel;
    private readonly List<AnimationClip> _animationClips = new();
    private readonly Dictionary<string, MorphClip> _morphClipsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MorphRuntimeMesh> _morphRuntimeMeshesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<RiggedModel.RiggedModelMesh, MorphRuntimeMesh> _morphRuntimeMeshesByMesh = new();
    private readonly MorphChannelSampler _morphChannelSampler = new();
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

        for (var morphClipIndex = 0; morphClipIndex < riggedModel.MorphClips.Count; morphClipIndex++)
        {
            var morphClip = riggedModel.MorphClips[morphClipIndex];
            if (!_morphClipsByName.ContainsKey(morphClip.Name))
            {
                _morphClipsByName.Add(morphClip.Name, morphClip);
            }
        }

        BuildMorphRuntimeMeshes();

        AnimationController = new AnimationController(SkeletonDefinition);
        AnimationController.AnimationEventTriggered += OnAnimationEventTriggered;
        ModelPose = new SkeletonPoseModel(SkeletonDefinition);
        SkinningPalette = new Matrix[riggedModel.GlobalShaderMatrixs.Length];
        DualQuaternionSkinningPalette = new Vector4[SkinningPalette.Length * 2];

        ResetSkinningPaletteToIdentity();
        RefreshEvaluatedPose();
    }

    public RiggedModel RiggedModel => _riggedModel;

    public SkeletonDefinition SkeletonDefinition { get; }

    public AnimationController AnimationController { get; }

    public SkeletonPoseLocal LocalPose => AnimationController.OutputPose;

    public SkeletonPoseModel ModelPose { get; }

    public IReadOnlyList<AnimationClip> AnimationClips => _animationClips;

    public IReadOnlyList<MorphTarget> MorphTargets => _riggedModel.MorphTargets;

    public IReadOnlyList<MorphClip> MorphClips => _riggedModel.MorphClips;

    public Matrix[] SkinningPalette { get; }

    public Vector4[] DualQuaternionSkinningPalette { get; }

    public bool CanUseDualQuaternionSkinning { get; private set; }

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

    public VertexBuffer? GetVertexBufferOverride(RiggedModel.RiggedModelMesh mesh, GraphicsDevice graphicsDevice, VertexDeclaration vertexDeclaration)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(vertexDeclaration);

        if (!_morphRuntimeMeshesByMesh.TryGetValue(mesh, out var morphRuntimeMesh) || !morphRuntimeMesh.HasActiveMorphs)
        {
            return null;
        }

        if (morphRuntimeMesh.Dirty || NeedsMorphVertexBufferUpload(morphRuntimeMesh, graphicsDevice))
        {
            if (!MorphVertexApplicator.Apply(mesh.Vertices, morphRuntimeMesh.MorphTargets, morphRuntimeMesh.CurrentAttachmentWeights, morphRuntimeMesh.WorkingVertices))
            {
                morphRuntimeMesh.HasActiveMorphs = false;
                morphRuntimeMesh.Dirty = false;
                return null;
            }

            EnsureMorphVertexBuffer(morphRuntimeMesh, graphicsDevice, vertexDeclaration);
            morphRuntimeMesh.VertexBuffer!.SetData(
                morphRuntimeMesh.WorkingVertices,
                0,
                morphRuntimeMesh.WorkingVertices.Length,
                SetDataOptions.Discard);
            morphRuntimeMesh.Dirty = false;
        }

        return morphRuntimeMesh.VertexBuffer;
    }

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

    public Matrix GetMeshNodeTransform(RiggedModel.RiggedModelMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return Matrix.Identity;
    }

    public void Dispose()
    {
        AnimationController.AnimationEventTriggered -= OnAnimationEventTriggered;

        foreach (var morphRuntimeMesh in _morphRuntimeMeshesByMesh.Values)
        {
            morphRuntimeMesh.VertexBuffer?.Dispose();
            morphRuntimeMesh.VertexBuffer = null;
        }
    }

    private void RefreshEvaluatedPose()
    {
        ModelPose.UpdateFromLocalPose(LocalPose);

        if (PosePostProcessing != null)
        {
            PosePostProcessing(LocalPose, ModelPose);
            ModelPose.UpdateFromLocalPose(LocalPose);
        }

        UpdateMorphWeights();
        UpdateSkinningPalette();
        CurrentAnimationFrameTime = AnimationController.CurrentTimeSeconds;
        AnimationRunning = AnimationController.IsPlaying;
        UpdateCurrentFrame();
    }

    private void BuildMorphRuntimeMeshes()
    {
        if (_riggedModel.Meshes == null || _riggedModel.Meshes.Length == 0 || _riggedModel.MorphTargets.Count == 0)
        {
            return;
        }

        var attachmentCapacities = new int[_riggedModel.Meshes.Length];
        var morphTargetCounts = new int[_riggedModel.Meshes.Length];

        for (var morphTargetIndex = 0; morphTargetIndex < _riggedModel.MorphTargets.Count; morphTargetIndex++)
        {
            var morphTarget = _riggedModel.MorphTargets[morphTargetIndex];
            if ((uint)morphTarget.MeshIndex >= (uint)_riggedModel.Meshes.Length)
            {
                continue;
            }

            morphTargetCounts[morphTarget.MeshIndex]++;
            attachmentCapacities[morphTarget.MeshIndex] = Math.Max(attachmentCapacities[morphTarget.MeshIndex], morphTarget.AttachmentIndex + 1);
        }

        var targetWriteOffsets = new int[_riggedModel.Meshes.Length];
        for (var meshIndex = 0; meshIndex < _riggedModel.Meshes.Length; meshIndex++)
        {
            if (morphTargetCounts[meshIndex] == 0)
            {
                continue;
            }

            var mesh = _riggedModel.Meshes[meshIndex];
            var morphRuntimeMesh = new MorphRuntimeMesh(mesh, meshIndex, morphTargetCounts[meshIndex], attachmentCapacities[meshIndex]);
            _morphRuntimeMeshesByMesh[mesh] = morphRuntimeMesh;

            if (!string.IsNullOrWhiteSpace(mesh.NameOfMesh) && !_morphRuntimeMeshesByName.ContainsKey(mesh.NameOfMesh))
            {
                _morphRuntimeMeshesByName.Add(mesh.NameOfMesh, morphRuntimeMesh);
            }
        }

        for (var morphTargetIndex = 0; morphTargetIndex < _riggedModel.MorphTargets.Count; morphTargetIndex++)
        {
            var morphTarget = _riggedModel.MorphTargets[morphTargetIndex];
            if ((uint)morphTarget.MeshIndex >= (uint)_riggedModel.Meshes.Length)
            {
                continue;
            }

            var mesh = _riggedModel.Meshes[morphTarget.MeshIndex];
            if (!_morphRuntimeMeshesByMesh.TryGetValue(mesh, out var morphRuntimeMesh))
            {
                continue;
            }

            morphRuntimeMesh.MorphTargets[targetWriteOffsets[morphTarget.MeshIndex]++] = morphTarget;
            if ((uint)morphTarget.AttachmentIndex < (uint)morphRuntimeMesh.DefaultAttachmentWeights.Length)
            {
                morphRuntimeMesh.DefaultAttachmentWeights[morphTarget.AttachmentIndex] = morphTarget.DefaultWeight;
            }
        }
    }

    private void UpdateMorphWeights()
    {
        if (_morphRuntimeMeshesByMesh.Count == 0)
        {
            return;
        }

        ResetMorphWeightsToDefaults(MorphWeightBufferKind.Current);

        if (AnimationController.HasAnimationGraph || AnimationController.CurrentState == null)
        {
            FinalizeMorphWeights();
            return;
        }

        if (!AnimationController.IsCrossFading || AnimationController.TargetState == null)
        {
            SampleMorphClip(AnimationController.CurrentState, MorphWeightBufferKind.Current);
            FinalizeMorphWeights();
            return;
        }

        ResetMorphWeightsToDefaults(MorphWeightBufferKind.Source);
        ResetMorphWeightsToDefaults(MorphWeightBufferKind.Target);
        SampleMorphClip(AnimationController.CurrentState, MorphWeightBufferKind.Source);
        SampleMorphClip(AnimationController.TargetState, MorphWeightBufferKind.Target);
        BlendCrossFadeMorphWeights(AnimationController.CrossFadeBlendWeight);
        FinalizeMorphWeights();
    }

    private void ResetMorphWeightsToDefaults(MorphWeightBufferKind bufferKind)
    {
        foreach (var morphRuntimeMesh in _morphRuntimeMeshesByMesh.Values)
        {
            var destinationWeights = GetMorphWeights(morphRuntimeMesh, bufferKind);
            Array.Copy(morphRuntimeMesh.DefaultAttachmentWeights, destinationWeights, destinationWeights.Length);
        }
    }

    private void SampleMorphClip(AnimationState animationState, MorphWeightBufferKind bufferKind)
    {
        if (!_morphClipsByName.TryGetValue(animationState.Clip.Name, out var morphClip))
        {
            return;
        }

        for (var channelIndex = 0; channelIndex < morphClip.Channels.Count; channelIndex++)
        {
            var morphChannel = morphClip.Channels[channelIndex];
            if (!TryResolveMorphRuntimeMesh(morphChannel, out var morphRuntimeMesh))
            {
                continue;
            }

            _morphChannelSampler.Sample(
                morphChannel,
                animationState.TimeSeconds,
                morphClip.DurationSeconds,
                animationState.Loop,
                GetMorphWeights(morphRuntimeMesh, bufferKind));
        }
    }

    private void BlendCrossFadeMorphWeights(float blendWeight)
    {
        foreach (var morphRuntimeMesh in _morphRuntimeMeshesByMesh.Values)
        {
            for (var weightIndex = 0; weightIndex < morphRuntimeMesh.CurrentAttachmentWeights.Length; weightIndex++)
            {
                morphRuntimeMesh.CurrentAttachmentWeights[weightIndex] = MathHelper.Lerp(
                    morphRuntimeMesh.SourceAttachmentWeights[weightIndex],
                    morphRuntimeMesh.TargetAttachmentWeights[weightIndex],
                    blendWeight);
            }
        }
    }

    private void FinalizeMorphWeights()
    {
        foreach (var morphRuntimeMesh in _morphRuntimeMeshesByMesh.Values)
        {
            morphRuntimeMesh.HasActiveMorphs = HasActiveMorphWeights(morphRuntimeMesh.CurrentAttachmentWeights);
            morphRuntimeMesh.Dirty = morphRuntimeMesh.HasActiveMorphs;
        }
    }

    private static bool HasActiveMorphWeights(float[] weights)
    {
        for (var weightIndex = 0; weightIndex < weights.Length; weightIndex++)
        {
            if (Math.Abs(weights[weightIndex]) > MorphWeightEpsilon)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveMorphRuntimeMesh(MorphChannel morphChannel, out MorphRuntimeMesh morphRuntimeMesh)
    {
        morphRuntimeMesh = null!;

        if ((uint)morphChannel.MeshIndex < (uint)_riggedModel.Meshes.Length)
        {
            var mesh = _riggedModel.Meshes[morphChannel.MeshIndex];
            if (_morphRuntimeMeshesByMesh.TryGetValue(mesh, out morphRuntimeMesh))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(morphChannel.MeshName))
        {
            return _morphRuntimeMeshesByName.TryGetValue(morphChannel.MeshName, out morphRuntimeMesh);
        }

        return false;
    }

    private static float[] GetMorphWeights(MorphRuntimeMesh morphRuntimeMesh, MorphWeightBufferKind bufferKind)
    {
        return bufferKind switch
        {
            MorphWeightBufferKind.Current => morphRuntimeMesh.CurrentAttachmentWeights,
            MorphWeightBufferKind.Source => morphRuntimeMesh.SourceAttachmentWeights,
            MorphWeightBufferKind.Target => morphRuntimeMesh.TargetAttachmentWeights,
            _ => throw new ArgumentOutOfRangeException(nameof(bufferKind), bufferKind, null),
        };
    }

    private static void EnsureMorphVertexBuffer(MorphRuntimeMesh morphRuntimeMesh, GraphicsDevice graphicsDevice, VertexDeclaration vertexDeclaration)
    {
        if (!NeedsMorphVertexBufferUpload(morphRuntimeMesh, graphicsDevice))
        {
            return;
        }

        morphRuntimeMesh.VertexBuffer?.Dispose();
        morphRuntimeMesh.VertexBuffer = new DynamicVertexBuffer(
            graphicsDevice,
            vertexDeclaration,
            morphRuntimeMesh.WorkingVertices.Length,
            BufferUsage.WriteOnly);
    }

    private static bool NeedsMorphVertexBufferUpload(MorphRuntimeMesh morphRuntimeMesh, GraphicsDevice graphicsDevice)
    {
        return morphRuntimeMesh.VertexBuffer == null
            || morphRuntimeMesh.VertexBuffer.IsDisposed
            || !ReferenceEquals(morphRuntimeMesh.VertexBuffer.GraphicsDevice, graphicsDevice)
            || morphRuntimeMesh.VertexBuffer.VertexCount < morphRuntimeMesh.WorkingVertices.Length;
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

        CanUseDualQuaternionSkinning = DualQuaternion.TryWriteSkinningPalette(SkinningPalette, DualQuaternionSkinningPalette);
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

    private enum MorphWeightBufferKind
    {
        Current,
        Source,
        Target,
    }

    private sealed class MorphRuntimeMesh
    {
        public MorphRuntimeMesh(RiggedModel.RiggedModelMesh mesh, int meshIndex, int morphTargetCount, int attachmentCapacity)
        {
            Mesh = mesh;
            MeshIndex = meshIndex;
            MorphTargets = new MorphTarget[morphTargetCount];
            DefaultAttachmentWeights = new float[attachmentCapacity];
            CurrentAttachmentWeights = new float[attachmentCapacity];
            SourceAttachmentWeights = new float[attachmentCapacity];
            TargetAttachmentWeights = new float[attachmentCapacity];
            WorkingVertices = new VertexPositionTextureNormalTangentWeights[mesh.Vertices.Length];
        }

        public RiggedModel.RiggedModelMesh Mesh { get; }

        public int MeshIndex { get; }

        public MorphTarget[] MorphTargets { get; }

        public float[] DefaultAttachmentWeights { get; }

        public float[] CurrentAttachmentWeights { get; }

        public float[] SourceAttachmentWeights { get; }

        public float[] TargetAttachmentWeights { get; }

        public VertexPositionTextureNormalTangentWeights[] WorkingVertices { get; }

        public DynamicVertexBuffer? VertexBuffer { get; set; }

        public bool HasActiveMorphs { get; set; }

        public bool Dirty { get; set; } = true;
    }
}