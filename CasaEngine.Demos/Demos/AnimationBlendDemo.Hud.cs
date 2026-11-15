using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>The text overlay of the animation blend demo, and the start-up settings read from the environment for automated checks.</summary>
public partial class AnimationBlendDemo
{
    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        var spriteBatch = game.Renderer2DComponent?.SpriteBatch;
        if (spriteBatch == null)
        {
            return;
        }

        var font = game.FontSystem.GetFont(14);
        _hudBuilder.Clear();
        _hudBuilder.AppendLine("=== Animation Showcase ===");
        _hudBuilder.AppendLine($"Mode: {GetModeLabel(_demoMode)}");
        _hudBuilder.AppendLine();

        switch (_demoMode)
        {
            case DemoMode.LinearBlendTree:
                _hudBuilder.AppendLine($"Blend: {_blendParameter1D:F2} (0=Idle, 1=Walk, 2=Run)");
                _hudBuilder.AppendLine("[Tab] Next page  [Shift+Tab] Previous page");
                _hudBuilder.AppendLine("[W] Walk  [Shift+W] Run  [Backspace] Reset actor");
                break;

            case DemoMode.BlendSpace1D:
                _hudBuilder.AppendLine($"Blend: {_blendParameter1D:F2} (0=Idle, 1=Walk, 2=Run)");
                _hudBuilder.AppendLine("[Tab] Next page  [Shift+Tab] Previous page");
                _hudBuilder.AppendLine("[W] Walk  [Shift+W] Run  [Backspace] Reset actor");
                break;

            case DemoMode.BlendSpace2D:
                _hudBuilder.AppendLine($"Blend: X={_blendParameter2D.X:F2}  Y={_blendParameter2D.Y:F2}");
                _hudBuilder.AppendLine("[Tab] Next page  [Shift+Tab] Previous page");
                _hudBuilder.AppendLine("[W/A/D] Explore strafe/forward directional locomotion  [Backspace] Reset actor");
                break;

            case DemoMode.CrossFade:
                _hudBuilder.AppendLine("[1] Idle  [2] Walk  [3] Run");
                _hudBuilder.AppendLine($"Cross-fade duration: {CrossFadeDurationSeconds:F2}s");
                _hudBuilder.AppendLine("[Tab] Next page  [Shift+Tab] Previous page");
                break;

            case DemoMode.AdvancedCrossFade:
                _hudBuilder.AppendLine("[1] Idle  [2] Walk  [3] Run");
                _hudBuilder.AppendLine($"Transition easing: {GetTransitionEasingLabel(_advancedCrossFadeEasingMode)}");
                _hudBuilder.AppendLine($"Preserve root velocity: {(_advancedCrossFadePreserveRootVelocity ? "On" : "Off")}");
                _hudBuilder.AppendLine($"Transition active: {(_riggedModel?.AnimationController?.IsCrossFading == true ? "Yes" : "No")}");
                _hudBuilder.AppendLine("[E] Cycle easing  [R] Toggle velocity preservation");
                _hudBuilder.AppendLine("[Tab] Next page  [Shift+Tab] Previous page");
                break;

            case DemoMode.LayeredUpperBody:
                _hudBuilder.AppendLine($"Base locomotion: {_blendParameter1D:F2}");
                _hudBuilder.AppendLine($"Upper-body layer weight: {_upperBodyLayerWeight:F2}");
                _hudBuilder.AppendLine("[W] Walk  [Shift+W] Run  [Space] Trigger upper-body action");
                _hudBuilder.AppendLine("[Q]/[E] Decrease/Increase layer weight");
                break;

            case DemoMode.AdditiveRootMotion:
                _hudBuilder.AppendLine($"Base locomotion: {_blendParameter1D:F2}");
                _hudBuilder.AppendLine($"Additive breathing weight: {_additiveLayerWeight:F2}");
                _hudBuilder.AppendLine($"Root motion mode: {(_applyRootMotionToEntity ? "Apply to entity" : "Observe only")}");
                _hudBuilder.AppendLine($"Last delta: {_lastObservedRootMotionDelta.Translation.X:F2}, {_lastObservedRootMotionDelta.Translation.Y:F2}, {_lastObservedRootMotionDelta.Translation.Z:F2}");
                _hudBuilder.AppendLine("[W] Walk  [Shift+W] Run  [Space] Trigger root-motion burst");
                _hudBuilder.AppendLine("[R] Toggle observe/apply  [Backspace] Reset trail and actor");
                break;
        }

        if (_latestEventTimeRemaining > 0f && !string.IsNullOrEmpty(_latestEventMessage))
        {
            _hudBuilder.AppendLine();
            _hudBuilder.AppendLine($"Event: {_latestEventMessage}");
        }

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        font.DrawText(spriteBatch, _hudBuilder.ToString(), new Vector2(10f, 10f), Color.White);
        spriteBatch.End();
    }

    private static string GetModeLabel(DemoMode mode)
    {
        return mode switch
        {
            DemoMode.LinearBlendTree => "Linear blend tree",
            DemoMode.BlendSpace1D => "Blend space 1D",
            DemoMode.BlendSpace2D => "Blend space 2D",
            DemoMode.CrossFade => "Cross-fade playback",
            DemoMode.AdvancedCrossFade => "Advanced cross-fade playback",
            DemoMode.LayeredUpperBody => "Upper-body override layer",
            DemoMode.AdditiveRootMotion => "Additive + root motion",
            _ => mode.ToString(),
        };
    }

    private static DemoMode ResolveInitialDemoMode()
    {
        var requestedMode = Environment.GetEnvironmentVariable(BlendModeEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(requestedMode)
            && Enum.TryParse<DemoMode>(requestedMode, ignoreCase: true, out var demoMode))
        {
            return demoMode;
        }

        return DemoMode.BlendSpace1D;
    }

    private static Vector2? ResolveConfiguredBlendSpace2DParameter()
    {
        if (!TryParseConfiguredFloat(BlendSpace2DXEnvironmentVariable, out var x)
            || !TryParseConfiguredFloat(BlendSpace2DYEnvironmentVariable, out var y))
        {
            return null;
        }

        return new Vector2(Math.Clamp(x, -1f, 1f), Math.Clamp(y, -1f, 1f));
    }

    private static bool TryParseConfiguredFloat(string environmentVariable, out float value)
    {
        var rawValue = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            value = 0f;
            return false;
        }

        return float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string GetTransitionEasingLabel(AnimationTransitionEasingMode easingMode)
    {
        return easingMode switch
        {
            AnimationTransitionEasingMode.SmoothStep => "Smooth step",
            AnimationTransitionEasingMode.EaseOutCubic => "Ease-out cubic",
            _ => "Linear",
        };
    }
}
