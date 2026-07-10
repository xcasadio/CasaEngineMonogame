using System;
using System.Text;

using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

public class AnimationIkDemo : Demo
{
    private readonly record struct IkChainSelection(int RootJointIndex, int MidJointIndex, int EndJointIndex, string Label);

    private const float CharacterScale = 0.1f;
    private const float TargetMoveSpeedFactor = 1.1f;
    private const float WeightAdjustSpeed = 0.75f;
    private const float AutoOrbitSpeed = 1.4f;
    private static readonly Quaternion CharacterFacingRotation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(180f));
    private static readonly Color ChainColor = new(80, 220, 156);
    private static readonly Color TargetColor = new(255, 204, 96);
    private static readonly Color PoleColor = new(96, 196, 255);
    private static readonly Color TargetLinkColor = new(255, 136, 72);
    private static readonly SkeletonDebugDrawOptions SkeletonDebugOptions = new(
        0.18f,
        true,
        new Color(255, 214, 96),
        new Color(255, 110, 110),
        new Color(110, 255, 150),
        new Color(96, 170, 255));

    private CasaEngineGame _game;
    private SkinnedMeshComponent _skinnedMeshComponent;
    private IkChainSelection _chain;
    private KeyboardState _previousKeyboardState;
    private Vector3 _targetModelPosition;
    private Vector3 _poleModelPosition;
    private Vector3 _orbitCenterModelPosition;
    private Vector3 _orbitForwardDirection;
    private Vector3 _orbitSideDirection;
    private Vector3 _orbitUpDirection;
    private float _chainLength;
    private float _ikWeight = 1f;
    private float _orbitAngle;
    private bool _ikEnabled = true;
    private bool _autoOrbit = true;
    private bool _showSkeleton = true;
    private readonly StringBuilder _hudBuilder = new(512);

    public override string Title => "Animation IK demo";

    public override string Description => "Real skinned-character IK showcase: a live two-bone chain is selected from the skeleton, then driven by a movable target with 3D debug visualization and runtime weight control.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;

        var world = game.GameManager.CurrentWorld;
        var entity = new Entity { Name = "Animation IK Character" };
        _skinnedMeshComponent = new SkinnedMeshComponent();
        entity.RootComponent = _skinnedMeshComponent;
        ResetCharacterTransform();

        var skinnedMesh = game.AssetContentManager.LoadFromFile<SkinnedMesh>("SkinnedMesh\\kid_idle.model");
        skinnedMesh.Initialize(game.AssetContentManager);
        _skinnedMeshComponent.SkinnedMesh = skinnedMesh;

        if (_skinnedMeshComponent.AnimationClips.Count > 0)
        {
            _skinnedMeshComponent.PlayAnimation(0);
        }

        var skeleton = _skinnedMeshComponent.SkeletonDefinition
                       ?? throw new InvalidOperationException("The IK demo requires a valid skeleton definition.");
        _chain = ResolveBestChain(skeleton);
        ResetTargetFromCurrentPose();
        ApplyCurrentIkConstraint();

        world.AddEntity(entity);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(Vector3.Backward * 18f + Vector3.Up * 11f, Vector3.Up * 4.5f, Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null || _skinnedMeshComponent == null || _skinnedMeshComponent.CurrentModelPose == null)
        {
            return;
        }

        var keyboard = _game.IsActive ? Keyboard.GetState() : new KeyboardState();
        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (IsNewKeyPress(keyboard, Keys.Space))
        {
            _autoOrbit = !_autoOrbit;
        }

        if (IsNewKeyPress(keyboard, Keys.I))
        {
            _ikEnabled = !_ikEnabled;
        }

        if (IsNewKeyPress(keyboard, Keys.V))
        {
            _showSkeleton = !_showSkeleton;
        }

        if (IsNewKeyPress(keyboard, Keys.Back))
        {
            ResetTargetFromCurrentPose();
        }

        if (IsNewKeyPress(keyboard, Keys.R))
        {
            ResetCharacterTransform();
            ResetTargetFromCurrentPose();
        }

        UpdateIkWeight(keyboard, elapsedSeconds);

        if (_autoOrbit)
        {
            UpdateAutoOrbitTarget(elapsedSeconds);
        }
        else
        {
            UpdateManualTarget(keyboard, elapsedSeconds);
        }

        ApplyCurrentIkConstraint();
        DrawDebugVisualization();

        _previousKeyboardState = keyboard;
    }

    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        var spriteBatch = game.Renderer2DComponent?.SpriteBatch;
        if (spriteBatch == null)
        {
            return;
        }

        var font = game.FontSystem.GetFont(14);
        _hudBuilder.Clear();
        _hudBuilder.AppendLine("=== Animation IK Demo ===");
        _hudBuilder.AppendLine($"Chain: {_chain.Label}");
        _hudBuilder.AppendLine($"IK: {(_ikEnabled ? "On" : "Off")}  Weight: {_ikWeight:F2}  Orbit: {(_autoOrbit ? "On" : "Off")}  Skeleton: {(_showSkeleton ? "On" : "Off")}");
        _hudBuilder.AppendLine($"Target (model): {_targetModelPosition.X:F2}, {_targetModelPosition.Y:F2}, {_targetModelPosition.Z:F2}");
        _hudBuilder.AppendLine($"Pole (model): {_poleModelPosition.X:F2}, {_poleModelPosition.Y:F2}, {_poleModelPosition.Z:F2}");
        _hudBuilder.AppendLine();
        _hudBuilder.AppendLine("[Space] Toggle auto orbit  [I] Toggle IK  [V] Toggle skeleton  [Backspace] Reset target  [R] Reset actor");
        _hudBuilder.AppendLine("[Left/Right/Up/Down] Move target in-chain plane  [PageUp/PageDown] Vertical");
        _hudBuilder.AppendLine("[O]/[P] Decrease/Increase IK weight");

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        font.DrawText(spriteBatch, _hudBuilder.ToString(), new Vector2(10f, 10f), Color.White);
        spriteBatch.End();
    }

    public override void Clean()
    {
        _skinnedMeshComponent?.ClearTwoBoneIkConstraints();
    }

    private void UpdateIkWeight(KeyboardState keyboard, float elapsedSeconds)
    {
        if (keyboard.IsKeyDown(Keys.O))
        {
            _ikWeight = Math.Clamp(_ikWeight - WeightAdjustSpeed * elapsedSeconds, 0f, 1f);
        }

        if (keyboard.IsKeyDown(Keys.P))
        {
            _ikWeight = Math.Clamp(_ikWeight + WeightAdjustSpeed * elapsedSeconds, 0f, 1f);
        }
    }

    private void UpdateAutoOrbitTarget(float elapsedSeconds)
    {
        _orbitAngle += elapsedSeconds * AutoOrbitSpeed;
        var horizontal = MathF.Cos(_orbitAngle) * (_chainLength * 0.18f);
        var vertical = MathF.Sin(_orbitAngle * 0.8f) * (_chainLength * 0.12f);
        var depth = MathF.Sin(_orbitAngle) * (_chainLength * 0.08f);

        _targetModelPosition = _orbitCenterModelPosition
                               + _orbitSideDirection * horizontal
                               + _orbitUpDirection * vertical
                               + _orbitForwardDirection * depth;
    }

    private void UpdateManualTarget(KeyboardState keyboard, float elapsedSeconds)
    {
        var movement = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.Left))
        {
            movement -= _orbitSideDirection;
        }

        if (keyboard.IsKeyDown(Keys.Right))
        {
            movement += _orbitSideDirection;
        }

        if (keyboard.IsKeyDown(Keys.Up))
        {
            movement += _orbitForwardDirection;
        }

        if (keyboard.IsKeyDown(Keys.Down))
        {
            movement -= _orbitForwardDirection;
        }

        if (keyboard.IsKeyDown(Keys.PageUp))
        {
            movement += _orbitUpDirection;
        }

        if (keyboard.IsKeyDown(Keys.PageDown))
        {
            movement -= _orbitUpDirection;
        }

        if (movement.LengthSquared() <= 0f)
        {
            return;
        }

        movement.Normalize();
        _targetModelPosition += movement * (_chainLength * TargetMoveSpeedFactor * elapsedSeconds);
    }

    private void ApplyCurrentIkConstraint()
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        if (!_ikEnabled || _ikWeight <= 0f)
        {
            _skinnedMeshComponent.ClearTwoBoneIkConstraint(0);
            return;
        }

        _skinnedMeshComponent.SetTwoBoneIkConstraint(
            0,
            new TwoBoneIkConstraint(
                _chain.RootJointIndex,
                _chain.MidJointIndex,
                _chain.EndJointIndex,
                _targetModelPosition,
                _poleModelPosition,
                _ikWeight,
                true));
    }

    private void DrawDebugVisualization()
    {
        if (_game == null || _skinnedMeshComponent == null || _skinnedMeshComponent.CurrentModelPose == null)
        {
            return;
        }

        var modelPose = _skinnedMeshComponent.CurrentModelPose;
        var rootWorld = ToWorld(modelPose.GetTransform(_chain.RootJointIndex).Translation);
        var midWorld = ToWorld(modelPose.GetTransform(_chain.MidJointIndex).Translation);
        var endWorld = ToWorld(modelPose.GetTransform(_chain.EndJointIndex).Translation);
        var targetWorld = ToWorld(_targetModelPosition);
        var poleWorld = ToWorld(_poleModelPosition);
        var lineRenderer = _game.Line3dRendererComponent;

        lineRenderer.AddLine(rootWorld, midWorld, ChainColor);
        lineRenderer.AddLine(midWorld, endWorld, ChainColor);
        lineRenderer.AddLine(endWorld, targetWorld, TargetLinkColor);
        lineRenderer.AddLine(midWorld, poleWorld, PoleColor);

        if (_showSkeleton)
        {
            SkeletonDebugVisualizer.Draw(lineRenderer, modelPose, _skinnedMeshComponent.WorldMatrixWithScale, SkeletonDebugOptions);
        }

        var gizmoSize = MathF.Max(_chainLength * CharacterScale * 0.06f, 0.08f);
        DrawCross(lineRenderer, targetWorld, gizmoSize, TargetColor);
        DrawCross(lineRenderer, poleWorld, gizmoSize * 0.85f, PoleColor);
    }

    private void DrawCross(Line3dRendererComponent lineRenderer, Vector3 center, float size, Color color)
    {
        lineRenderer.AddLine(center - Vector3.Right * size, center + Vector3.Right * size, color);
        lineRenderer.AddLine(center - Vector3.Up * size, center + Vector3.Up * size, color);
        lineRenderer.AddLine(center - Vector3.Forward * size, center + Vector3.Forward * size, color);
    }

    private void ResetTargetFromCurrentPose()
    {
        if (_skinnedMeshComponent?.CurrentModelPose == null)
        {
            return;
        }

        var modelPose = _skinnedMeshComponent.CurrentModelPose;
        var rootPosition = modelPose.GetTransform(_chain.RootJointIndex).Translation;
        var midPosition = modelPose.GetTransform(_chain.MidJointIndex).Translation;
        var endPosition = modelPose.GetTransform(_chain.EndJointIndex).Translation;

        _chainLength = Vector3.Distance(rootPosition, midPosition) + Vector3.Distance(midPosition, endPosition);
        _orbitForwardDirection = SafeNormalize(endPosition - rootPosition, Vector3.UnitX);
        _orbitSideDirection = SafeNormalize(
            Vector3.Cross(_orbitForwardDirection, Vector3.Up),
            Vector3.Cross(_orbitForwardDirection, Vector3.Forward));
        if (Vector3.Dot(_orbitSideDirection, rootPosition) < 0f)
        {
            _orbitSideDirection = -_orbitSideDirection;
        }

        _orbitUpDirection = SafeNormalize(Vector3.Cross(_orbitSideDirection, _orbitForwardDirection), Vector3.Up);

        _orbitCenterModelPosition = endPosition
                                    + _orbitSideDirection * (_chainLength * 0.62f)
                                    + _orbitUpDirection * (_chainLength * 0.22f)
                                    + _orbitForwardDirection * (_chainLength * 0.08f);
        _targetModelPosition = _orbitCenterModelPosition;
        _poleModelPosition = midPosition
                             + _orbitSideDirection * (_chainLength * 1.05f)
                             + _orbitUpDirection * (_chainLength * 0.2f);
        _orbitAngle = 0f;
        UpdateAutoOrbitTarget(0f);
    }

    private void ResetCharacterTransform()
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        _skinnedMeshComponent.LocalPosition = Vector3.Zero;
        _skinnedMeshComponent.LocalOrientation = CharacterFacingRotation;
        _skinnedMeshComponent.LocalScale = new Vector3(CharacterScale);
    }

    private Vector3 ToWorld(Vector3 modelPosition)
    {
        return Vector3.Transform(modelPosition, _skinnedMeshComponent.WorldMatrixWithScale);
    }

    private static IkChainSelection ResolveBestChain(SkeletonDefinition skeleton)
    {
        var foundArmChain = false;
        var bestArmScore = int.MinValue;
        var bestArmLength = float.MinValue;
        var bestArmChain = default(IkChainSelection);
        var foundFallbackChain = false;
        var bestFallbackScore = int.MinValue;
        var bestFallbackLength = float.MinValue;
        var bestFallbackChain = default(IkChainSelection);

        for (var endJointIndex = 0; endJointIndex < skeleton.Count; endJointIndex++)
        {
            var midJointIndex = skeleton.GetJoint(endJointIndex).ParentIndex;
            if (midJointIndex < 0)
            {
                continue;
            }

            var rootJointIndex = skeleton.GetJoint(midJointIndex).ParentIndex;
            if (rootJointIndex < 0)
            {
                continue;
            }

            var rootName = skeleton.GetJoint(rootJointIndex).Name;
            var midName = skeleton.GetJoint(midJointIndex).Name;
            var endName = skeleton.GetJoint(endJointIndex).Name;
            if (IsHelperChain(rootName, midName, endName))
            {
                continue;
            }

            var label = $"{rootName} -> {midName} -> {endName}";
            var chainLength = GetChainBindLength(skeleton, midJointIndex, endJointIndex);
            var armScore = ScoreArmChain(rootName, midName, endName);
            if (armScore > 0
                && (!foundArmChain
                    || armScore > bestArmScore
                    || (armScore == bestArmScore && chainLength > bestArmLength)))
            {
                bestArmScore = armScore;
                bestArmLength = chainLength;
                bestArmChain = new IkChainSelection(
                    rootJointIndex,
                    midJointIndex,
                    endJointIndex,
                    label);
                foundArmChain = true;
            }

            var fallbackScore = Math.Max(ScoreLegChain(rootName, midName, endName), 0);
            if (!foundFallbackChain
                || fallbackScore > bestFallbackScore
                || (fallbackScore == bestFallbackScore && chainLength > bestFallbackLength))
            {
                bestFallbackScore = fallbackScore;
                bestFallbackLength = chainLength;
                bestFallbackChain = new IkChainSelection(
                    rootJointIndex,
                    midJointIndex,
                    endJointIndex,
                    label);
                foundFallbackChain = true;
            }
        }

        if (foundArmChain)
        {
            return bestArmChain;
        }

        if (!foundFallbackChain)
        {
            throw new InvalidOperationException("The IK demo could not find a valid two-bone chain in the skeleton.");
        }

        return bestFallbackChain;
    }

    private static bool IsHelperChain(string rootName, string midName, string endName)
    {
        return IsHelperJoint(rootName) || IsHelperJoint(midName) || IsHelperJoint(endName);
    }

    private static bool IsHelperJoint(string jointName)
    {
        return jointName.Contains("nub", StringComparison.OrdinalIgnoreCase)
               || jointName.Contains("twist", StringComparison.OrdinalIgnoreCase)
               || jointName.Contains("helper", StringComparison.OrdinalIgnoreCase);
    }

    private static float GetChainBindLength(SkeletonDefinition skeleton, int midJointIndex, int endJointIndex)
    {
        return skeleton.GetJoint(midJointIndex).LocalBindTransform.Translation.Length()
               + skeleton.GetJoint(endJointIndex).LocalBindTransform.Translation.Length();
    }

    private static int ScoreArmChain(string rootName, string midName, string endName)
    {
        var score = 0;
        score += ScoreName(rootName, "shoulder", "upperarm", "arm", "clavicle") * 6;
        score += ScoreName(midName, "forearm", "lowerarm", "elbow") * 8;
        score += ScoreName(endName, "hand", "wrist") * 10;
        score += ScoreSide(rootName, midName, endName);
        return score;
    }

    private static int ScoreLegChain(string rootName, string midName, string endName)
    {
        var score = 0;
        score += ScoreName(rootName, "thigh", "upleg", "leg", "hip") * 6;
        score += ScoreName(midName, "calf", "lowerleg", "shin", "knee") * 8;
        score += ScoreName(endName, "foot", "ankle", "toe") * 10;
        score += ScoreSide(rootName, midName, endName);
        return score;
    }

    private static int ScoreSide(string rootName, string midName, string endName)
    {
        var score = 0;
        score += ScoreName(rootName, "right", "r_", ".r", "_r") * 2;
        score += ScoreName(midName, "right", "r_", ".r", "_r") * 2;
        score += ScoreName(endName, "right", "r_", ".r", "_r") * 2;
        return score;
    }

    private static int ScoreName(string jointName, params string[] keywords)
    {
        if (string.IsNullOrEmpty(jointName))
        {
            return 0;
        }

        var score = 0;
        for (var keywordIndex = 0; keywordIndex < keywords.Length; keywordIndex++)
        {
            if (jointName.Contains(keywords[keywordIndex], StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }
        }

        return score;
    }

    private bool IsNewKeyPress(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    private static Vector3 SafeNormalize(Vector3 vector, Vector3 fallback)
    {
        if (vector.LengthSquared() <= 1e-6f)
        {
            if (fallback.LengthSquared() <= 1e-6f)
            {
                return Vector3.UnitX;
            }

            return Vector3.Normalize(fallback);
        }

        return Vector3.Normalize(vector);
    }
}