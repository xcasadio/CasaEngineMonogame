using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Uploads visible forward-light data from <see cref="LightingContext"/> into a shader.
/// The binder owns reusable temporary arrays so the hot draw path does not allocate.
/// </summary>
public sealed class ForwardLightBinder
{
    private static readonly Vector3 ZeroVector3 = Vector3.Zero;
    private static readonly Vector4 ZeroVector4 = Vector4.Zero;

    private readonly Vector3[] _directionalLightDirections = new Vector3[LightingContext.MaxDirectionalLights];
    private readonly Vector3[] _directionalLightDiffuseColors = new Vector3[LightingContext.MaxDirectionalLights];
    private readonly Vector3[] _directionalLightSpecularColors = new Vector3[LightingContext.MaxDirectionalLights];
    private readonly Vector4[] _pointLightPositionAndRangeData = new Vector4[LightingContext.MaxPointLights];
    private readonly Vector4[] _pointLightDiffuseData = new Vector4[LightingContext.MaxPointLights];
    private readonly Vector4[] _pointLightSpecularData = new Vector4[LightingContext.MaxPointLights];
    private readonly Vector4[] _spotLightPositionAndRangeData = new Vector4[LightingContext.MaxSpotLights];
    private readonly Vector4[] _spotLightDirectionAndInnerConeCosData = new Vector4[LightingContext.MaxSpotLights];
    private readonly Vector4[] _spotLightDiffuseData = new Vector4[LightingContext.MaxSpotLights];
    private readonly Vector4[] _spotLightSpecularAndOuterConeCosData = new Vector4[LightingContext.MaxSpotLights];

    public void Bind(ShaderWrapper shader, LightingContext? lighting, RenderStats? stats = null)
    {
        ArgumentNullException.ThrowIfNull(shader);

        PopulateBindingData(
            lighting,
            _directionalLightDirections,
            _directionalLightDiffuseColors,
            _directionalLightSpecularColors,
            _pointLightPositionAndRangeData,
            _pointLightDiffuseData,
            _pointLightSpecularData,
            _spotLightPositionAndRangeData,
            _spotLightDirectionAndInnerConeCosData,
            _spotLightDiffuseData,
            _spotLightSpecularAndOuterConeCosData,
            out int activeDirectionalLightCount,
            out int activePointLightCount,
            out int activeSpotLightCount,
            out Vector3 ambientColor);

        shader.SetParameter(ShaderParameterNames.ActiveDirectionalLightCount, (float)activeDirectionalLightCount);
        shader.SetParameter(ShaderParameterNames.ActivePointLightCount, (float)activePointLightCount);
        shader.SetParameter(ShaderParameterNames.ActiveSpotLightCount, (float)activeSpotLightCount);

        for (int i = 0; i < LightingContext.MaxDirectionalLights; i++)
        {
            shader.SetParameter(ShaderParameterNames.DirectionalLightDirectionParameters[i], _directionalLightDirections[i]);
            shader.SetParameter(ShaderParameterNames.DirectionalLightDiffuseParameters[i], _directionalLightDiffuseColors[i]);
            shader.SetParameter(ShaderParameterNames.DirectionalLightSpecularParameters[i], _directionalLightSpecularColors[i]);
        }

        shader.SetParameter(ShaderParameterNames.PointLightPositionAndRange, _pointLightPositionAndRangeData);
        shader.SetParameter(ShaderParameterNames.PointLightDiffuseColors, _pointLightDiffuseData);
        shader.SetParameter(ShaderParameterNames.PointLightSpecularColors, _pointLightSpecularData);

        shader.SetParameter(ShaderParameterNames.SpotLightPositionAndRange, _spotLightPositionAndRangeData);
        shader.SetParameter(ShaderParameterNames.SpotLightDirectionAndInnerConeCos, _spotLightDirectionAndInnerConeCosData);
        shader.SetParameter(ShaderParameterNames.SpotLightDiffuseColors, _spotLightDiffuseData);
        shader.SetParameter(ShaderParameterNames.SpotLightSpecularColorsAndOuterConeCos, _spotLightSpecularAndOuterConeCosData);
        shader.SetParameter(ShaderParameterNames.AmbientColor, ambientColor);
    }

    internal static ForwardLightBindingSnapshot CreateSnapshot(LightingContext? lighting)
    {
        var snapshot = new ForwardLightBindingSnapshot();
        PopulateBindingData(
            lighting,
            snapshot.DirectionalLightDirections,
            snapshot.DirectionalLightDiffuseColors,
            snapshot.DirectionalLightSpecularColors,
            snapshot.PointLightPositionAndRangeData,
            snapshot.PointLightDiffuseData,
            snapshot.PointLightSpecularData,
            snapshot.SpotLightPositionAndRangeData,
            snapshot.SpotLightDirectionAndInnerConeCosData,
            snapshot.SpotLightDiffuseData,
            snapshot.SpotLightSpecularAndOuterConeCosData,
            out int activeDirectionalLightCount,
            out int activePointLightCount,
            out int activeSpotLightCount,
            out Vector3 ambientColor);

        snapshot.ActiveDirectionalLightCount = activeDirectionalLightCount;
        snapshot.ActivePointLightCount = activePointLightCount;
        snapshot.ActiveSpotLightCount = activeSpotLightCount;
        snapshot.AmbientColor = ambientColor;
        return snapshot;
    }

    private static void PopulateBindingData(
        LightingContext? lighting,
        Vector3[] directionalLightDirections,
        Vector3[] directionalLightDiffuseColors,
        Vector3[] directionalLightSpecularColors,
        Vector4[] pointLightPositionAndRangeData,
        Vector4[] pointLightDiffuseData,
        Vector4[] pointLightSpecularData,
        Vector4[] spotLightPositionAndRangeData,
        Vector4[] spotLightDirectionAndInnerConeCosData,
        Vector4[] spotLightDiffuseData,
        Vector4[] spotLightSpecularAndOuterConeCosData,
        out int activeDirectionalLightCount,
        out int activePointLightCount,
        out int activeSpotLightCount,
        out Vector3 ambientColor)
    {
        activeDirectionalLightCount = lighting is null
            ? 0
            : LightingContext.ClampActiveDirectionalLightCount(lighting.ActiveDirectionalLightCount);
        activePointLightCount = lighting is null
            ? 0
            : LightingContext.ClampActivePointLightCount(lighting.ActivePointLightCount);
        activeSpotLightCount = lighting is null
            ? 0
            : LightingContext.ClampActiveSpotLightCount(lighting.ActiveSpotLightCount);
        ambientColor = lighting?.AmbientColor ?? Vector3.Zero;

        for (int i = 0; i < LightingContext.MaxDirectionalLights; i++)
        {
            if (lighting != null && i < activeDirectionalLightCount)
            {
                var directionalLight = lighting.DirectionalLights[i];
                directionalLightDirections[i] = directionalLight.Direction;
                directionalLightDiffuseColors[i] = directionalLight.DiffuseColor * directionalLight.Intensity;
                directionalLightSpecularColors[i] = directionalLight.SpecularColor * directionalLight.Intensity;
            }
            else
            {
                directionalLightDirections[i] = ZeroVector3;
                directionalLightDiffuseColors[i] = ZeroVector3;
                directionalLightSpecularColors[i] = ZeroVector3;
            }
        }

        for (int i = 0; i < LightingContext.MaxPointLights; i++)
        {
            if (lighting != null && i < activePointLightCount)
            {
                var pointLight = lighting.PointLights[i];
                pointLightPositionAndRangeData[i] = new Vector4(pointLight.Position, pointLight.Range);
                pointLightDiffuseData[i] = new Vector4(pointLight.DiffuseColor * pointLight.Intensity, 0.0f);
                pointLightSpecularData[i] = new Vector4(pointLight.SpecularColor * pointLight.Intensity, 0.0f);
            }
            else
            {
                pointLightPositionAndRangeData[i] = ZeroVector4;
                pointLightDiffuseData[i] = ZeroVector4;
                pointLightSpecularData[i] = ZeroVector4;
            }
        }

        for (int i = 0; i < LightingContext.MaxSpotLights; i++)
        {
            if (lighting != null && i < activeSpotLightCount)
            {
                var spotLight = lighting.SpotLights[i];
                spotLightPositionAndRangeData[i] = new Vector4(spotLight.Position, spotLight.Range);
                spotLightDirectionAndInnerConeCosData[i] = new Vector4(spotLight.Direction, MathF.Cos(spotLight.InnerConeAngle));
                spotLightDiffuseData[i] = new Vector4(spotLight.DiffuseColor * spotLight.Intensity, 0.0f);
                spotLightSpecularAndOuterConeCosData[i] = new Vector4(
                    spotLight.SpecularColor * spotLight.Intensity,
                    MathF.Cos(spotLight.OuterConeAngle));
            }
            else
            {
                spotLightPositionAndRangeData[i] = ZeroVector4;
                spotLightDirectionAndInnerConeCosData[i] = ZeroVector4;
                spotLightDiffuseData[i] = ZeroVector4;
                spotLightSpecularAndOuterConeCosData[i] = ZeroVector4;
            }
        }
    }
}

internal sealed class ForwardLightBindingSnapshot
{
    public int ActiveDirectionalLightCount { get; set; }
    public int ActivePointLightCount { get; set; }
    public int ActiveSpotLightCount { get; set; }
    public Vector3 AmbientColor { get; set; }

    public Vector3[] DirectionalLightDirections { get; } = new Vector3[LightingContext.MaxDirectionalLights];
    public Vector3[] DirectionalLightDiffuseColors { get; } = new Vector3[LightingContext.MaxDirectionalLights];
    public Vector3[] DirectionalLightSpecularColors { get; } = new Vector3[LightingContext.MaxDirectionalLights];
    public Vector4[] PointLightPositionAndRangeData { get; } = new Vector4[LightingContext.MaxPointLights];
    public Vector4[] PointLightDiffuseData { get; } = new Vector4[LightingContext.MaxPointLights];
    public Vector4[] PointLightSpecularData { get; } = new Vector4[LightingContext.MaxPointLights];
    public Vector4[] SpotLightPositionAndRangeData { get; } = new Vector4[LightingContext.MaxSpotLights];
    public Vector4[] SpotLightDirectionAndInnerConeCosData { get; } = new Vector4[LightingContext.MaxSpotLights];
    public Vector4[] SpotLightDiffuseData { get; } = new Vector4[LightingContext.MaxSpotLights];
    public Vector4[] SpotLightSpecularAndOuterConeCosData { get; } = new Vector4[LightingContext.MaxSpotLights];
}