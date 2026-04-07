using CasaEngine.Core.Log;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Wraps a MonoGame <see cref="Effect"/> and caches <see cref="EffectParameter"/> lookups
/// so each parameter is resolved only once, then accessed via the cached reference.
/// </summary>
public class ShaderWrapper
{
    private readonly Effect _effect;
    private readonly Dictionary<string, EffectParameter?> _paramCache = new();

    public Effect Effect => _effect;

    public ShaderWrapper(Effect effect)
    {
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
    }

    /// <summary>Returns the cached <see cref="EffectParameter"/> or null if not found.</summary>
    public EffectParameter? GetParameter(string name)
    {
        if (!_paramCache.TryGetValue(name, out var param))
        {
            param = _effect.Parameters[name]; // null if absent — that's fine
            _paramCache[name] = param;
        }
        return param;
    }

    public bool HasParameter(string name) => GetParameter(name) != null;

    public bool HasTechnique(string techniqueName) => _effect.Techniques[techniqueName] != null;

    // --- Typed setters (no-op if parameter doesn't exist) ---

    public void SetParameter(string name, float value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Vector2 value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Vector3 value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Vector4 value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Color value) => GetParameter(name)?.SetValue(value.ToVector4());
    public void SetParameter(string name, Matrix value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Matrix[] value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, Texture2D? value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, TextureCube? value) => GetParameter(name)?.SetValue(value);
    public void SetParameter(string name, bool value) => GetParameter(name)?.SetValue(value);

    public void SetTextureParameter(string name, Texture2D? value, RenderStats? stats = null)
    {
        var parameter = GetParameter(name);
        if (parameter is null)
        {
            return;
        }

        parameter.SetValue(value);

        if (stats is not null)
        {
            stats.TextureBinds++;
        }
    }

    public void SetTextureCubeParameter(string name, TextureCube? value, RenderStats? stats = null)
    {
        var parameter = GetParameter(name);
        if (parameter is null)
        {
            return;
        }

        parameter.SetValue(value);

        if (stats is not null)
        {
            stats.TextureBinds++;
        }
    }

    /// <summary>
    /// Selects a named technique. Falls back to the first technique with a warning if not found.
    /// </summary>
    public void SelectTechnique(string techniqueName)
    {
        var technique = _effect.Techniques[techniqueName];
        if (technique != null)
        {
            _effect.CurrentTechnique = technique;
        }
        else
        {
            Logs.WriteWarning($"[ShaderWrapper] Technique '{techniqueName}' not found in effect '{_effect.Name}'. Falling back to default technique.");
        }
    }

    /// <summary>Applies the current technique's pass at <paramref name="passIndex"/>.</summary>
    public void ApplyPass(int passIndex = 0)
    {
        _effect.CurrentTechnique.Passes[passIndex].Apply();
    }

    /// <summary>Number of passes in the current technique.</summary>
    public int PassCount => _effect.CurrentTechnique.Passes.Count;
}
