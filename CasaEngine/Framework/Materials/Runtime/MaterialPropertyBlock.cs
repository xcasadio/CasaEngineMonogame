using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Materials.Runtime;

/// <summary>
/// Allows per-instance override of individual shader parameters without duplicating
/// the entire <see cref="MaterialBase"/> asset.
///
/// Typical use-cases: per-entity tint colour, selection highlight, dissolve effect.
///
/// Usage:
/// <code>
/// var block = new MaterialPropertyBlock();
/// block.SetColor("TintColor", Color.Red);
/// meshRenderer.AddMesh(mesh, world, wit, block);
/// </code>
/// </summary>
public sealed class MaterialPropertyBlock
{
    private readonly Dictionary<string, object> _properties = new();

    // -----------------------------------------------------------------------
    //  Setters
    // -----------------------------------------------------------------------

    public void SetFloat(string name, float value) => _properties[name] = value;
    public void SetVector2(string name, Vector2 value) => _properties[name] = value;
    public void SetVector3(string name, Vector3 value) => _properties[name] = value;
    public void SetVector4(string name, Vector4 value) => _properties[name] = value;
    public void SetColor(string name, Color value) => _properties[name] = value.ToVector4();
    public void SetTexture(string name, Texture2D value) => _properties[name] = value!;
    public void SetMatrix(string name, Matrix value) => _properties[name] = value;
    public void SetBool(string name, bool value) => _properties[name] = value;

    // -----------------------------------------------------------------------
    //  Getters
    // -----------------------------------------------------------------------

    public bool TryGetFloat(string name, out float value)
    {
        if (_properties.TryGetValue(name, out var raw) && raw is float f)
        {
            value = f; return true;
        }
        value = default;
        return false;
    }

    public bool TryGetVector3(string name, out Vector3 value)
    {
        if (_properties.TryGetValue(name, out var raw) && raw is Vector3 v)
        {
            value = v; return true;
        }
        value = default;
        return false;
    }

    public bool TryGetVector4(string name, out Vector4 value)
    {
        if (_properties.TryGetValue(name, out var raw) && raw is Vector4 v)
        {
            value = v; return true;
        }
        value = default; 
        return false;
    }

    // -----------------------------------------------------------------------
    //  Apply
    // -----------------------------------------------------------------------

    /// <summary>
    /// Pushes all overridden properties onto <paramref name="shader"/>.
    /// Call this <em>after</em> <see cref="MaterialBase.Bind"/> so per-instance values
    /// win over the material default.
    /// </summary>
    public void Apply(ShaderWrapper shader, RenderStats stats = null)
    {
        foreach (var (name, value) in _properties)
        {
            switch (value)
            {
                case float f: shader.SetParameter(name, f); break;
                case Vector2 v2: shader.SetParameter(name, v2); break;
                case Vector3 v3: shader.SetParameter(name, v3); break;
                case Vector4 v4: shader.SetParameter(name, v4); break;
                case Matrix m: shader.SetParameter(name, m); break;
                case Texture2D tex: shader.SetTextureParameter(name, tex, stats); break;
                case bool b: shader.SetParameter(name, b); break;
            }
        }
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    public void Clear() => _properties.Clear();
    public bool IsEmpty => _properties.Count == 0;
    public int PropertyCount => _properties.Count;
}
