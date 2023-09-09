using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Materials;

public class ShaderWriter : IMaterialAssetVisitor
{
    //structures
    private HashSet<Tuple<VertexElementFormat, VertexElementUsage>> _structureProperties = new()
    {
        new Tuple<VertexElementFormat, VertexElementUsage>(VertexElementFormat.Vector3, VertexElementUsage.Position)
    };

    private string _constants = string.Empty;
    private string _vertexDiffuseComputation = string.Empty;
    private string _vertexFunction = string.Empty;
    private string _pixelCustomFunctions = string.Empty;
    private string _pixelFunction = string.Empty;
    private string _technique = string.Empty;

    public string Compile(Material material)
    {
        material.Diffuse.Accept(this);

        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine(GetDefaultMacros());
        stringBuilder.AppendLine();

        stringBuilder.AppendLine(GetDefaultConstants());
        stringBuilder.AppendLine(_constants);
        stringBuilder.AppendLine();

        stringBuilder.AppendLine(GetStructures());
        stringBuilder.AppendLine();

        //stringBuilder.AppendLine(GetVertexCustomFunctions());
        //stringBuilder.AppendLine();
        stringBuilder.AppendLine(GetVertexFunction());
        stringBuilder.AppendLine();

        stringBuilder.AppendLine(GetPixelCustomFunctions());
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(GetPixelFunction());
        stringBuilder.AppendLine();

        stringBuilder.AppendLine(GetTechnique());

        return stringBuilder.ToString();
    }

    private void VisitCommon(MaterialAsset material)
    {
        foreach (var property in material.GetVertexPropertiesNeeded())
        {
            _structureProperties.Add(property);
        }
    }

    public void Visit(MaterialColor materialColor)
    {
        VisitCommon(materialColor);

        _pixelCustomFunctions = materialColor.WriteColor();
    }

    public void Visit(MaterialTexture materialTexture)
    {
        VisitCommon(materialTexture);

        _constants += materialTexture.GetConstant() + Environment.NewLine;
        _vertexFunction += materialTexture.GetVertexComputation() + Environment.NewLine;
        _pixelCustomFunctions = materialTexture.GetTextureColorFromUv() + Environment.NewLine;
    }

    private static string NewLine()
    {
        return Environment.NewLine;
    }

    private string GetDefaultMacros()
    {
        return @"//Macros
#define DECLARE_TEXTURE(Name, index) \
    Texture2D<float4> Name : register(t##index); \
    sampler Name##Sampler : register(s##index)

#define DECLARE_CUBEMAP(Name, index) \
    TextureCube<float4> Name : register(t##index); \
    sampler Name##Sampler : register(s##index)

#define SAMPLE_TEXTURE(Name, texCoord)  Name.Sample(Name##Sampler, texCoord)
#define SAMPLE_CUBEMAP(Name, texCoord)  Name.Sample(Name##Sampler, texCoord)

#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0
#endif

#define TECHNIQUE(name, vsname, psname ) \
	technique name { pass { VertexShader = compile VS_SHADERMODEL vsname (); PixelShader = compile PS_SHADERMODEL psname(); } }";
    }

    private string GetDefaultConstants()
    {
        return @"//Contants
//float4 DiffuseColor;
float4x4 WorldViewProj;";
    }

    private string GetStructures()
    {
        var result = string.Empty;

        var VertexElementUsageSlots = new Dictionary<VertexElementUsage, int>();

        foreach (var vertexElementUsage in Enum.GetValues<VertexElementUsage>())
        {
            VertexElementUsageSlots[vertexElementUsage] = -1;
        }

        result = @"//Structures
struct VSInput
{
";

        foreach (var property in _structureProperties)
        {
            VertexElementUsageSlots[property.Item2]++;
            var vertexElementUsageSlot = VertexElementUsageSlots[property.Item2];
            var typeName = ConvertType(property.Item1);
            var propertyName = $"{Enum.GetName(property.Item2)}{vertexElementUsageSlot}";
            var usageName = $"{ConvertUsage(property.Item2)}{(property.Item2 == VertexElementUsage.Position ? string.Empty : vertexElementUsageSlot)}";
            result += $"{typeName} {propertyName} : {usageName};" + Environment.NewLine;
        }

        result += "};";

        result += Environment.NewLine + Environment.NewLine;

        result += @"struct VSOutput
{
";

        var outputUsages = new[]
            {
                VertexElementUsage.Position,
                VertexElementUsage.Color,
                VertexElementUsage.TextureCoordinate
            };

        foreach (var property in _structureProperties
                     .Where(x => VertexElementUsageSlots[x.Item2] > -1 && outputUsages.Any(y => y == x.Item2)))
        {
            for (int i = 0; i <= VertexElementUsageSlots[property.Item2]; i++)
            {
                var typeName = ConvertType(property.Item2 == VertexElementUsage.Position ? VertexElementFormat.Vector4 : property.Item1);
                var propertyName = $"{Enum.GetName(property.Item2)}{i}";
                var usageName = $"{ConvertUsage(property.Item2)}{(property.Item2 == VertexElementUsage.Position ? string.Empty : i)}";
                result += $"{typeName} {propertyName} : {usageName};" + Environment.NewLine;
            }
        }

        result += "};" + Environment.NewLine;

        return result;
    }

    private string ConvertUsage(VertexElementUsage usage)
    {
        //see https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-semantics

        return usage switch
        {
            VertexElementUsage.Position => "SV_Position",
            VertexElementUsage.Color => "COLOR", // SV_Target[]
            VertexElementUsage.TextureCoordinate => "TEXCOORD",
            VertexElementUsage.Normal => "NORMAL",
            VertexElementUsage.Binormal => "BINORMAL",
            VertexElementUsage.Tangent => "TANGENT",
            VertexElementUsage.BlendIndices => "BLENDINDICES",
            VertexElementUsage.BlendWeight => "BLENDWEIGHT",
            VertexElementUsage.Depth => "DEPTH", // vertex output, only pixel shader input
            VertexElementUsage.Fog => "FOG", // vertex output, only pixel shader input
            VertexElementUsage.PointSize => "PSIZE",
            VertexElementUsage.Sample => "SV_SampleIndex",
            VertexElementUsage.TessellateFactor => "TESSFACTOR" // vertex output
            ,
            _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null)
        };
    }

    private string ConvertType(VertexElementFormat format)
    {
        return format switch
        {
            VertexElementFormat.Single => "float",
            VertexElementFormat.Vector2 => "float2",
            VertexElementFormat.Vector3 => "float3",
            VertexElementFormat.Vector4 => "float4",
            VertexElementFormat.Color => "float4",
            VertexElementFormat.Byte4 => "byte4",
            VertexElementFormat.Short2 => "short2",
            VertexElementFormat.Short4 => "short4",
            VertexElementFormat.NormalizedShort2 => "short2", // TODO
            VertexElementFormat.NormalizedShort4 => "short4", // TODO
            VertexElementFormat.HalfVector2 => "half2",
            VertexElementFormat.HalfVector4 => "half4",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    private string GetVertexCustomFunctions()
    {
        var code = @"//custom vertex functions
float4 GetVertexDiffuseColor(VSInput vin)
{
    return ##VertexDiffuseColorComputation##;
}";

        code = code.Replace("##VertexDiffuseColorComputation##", _vertexDiffuseComputation);
        return code;
    }

    private string GetVertexFunction()
    {
        var code = @"//Vertex shader
VSOutput VSBasic(VSInput vin)
{
    VSOutput vout;
	
    vout.Position0 = mul(float4(vin.Position0, 1.0), WorldViewProj);
    
    ##VertexFunctionCode##

    return vout;
}";
        code = code.Replace("##VertexFunctionCode##", _vertexFunction);

        return code;
    }
    private string GetPixelCustomFunctions()
    {
        var code = @"//custom pixel functions
float4 GetPixelDiffuseColor(VSOutput pin)
{
    return ##PixelDiffuseColorComputation##;
}";

        code = code.Replace("##PixelDiffuseColorComputation##", _pixelCustomFunctions);
        return code;
    }

    private string GetPixelFunction()
    {
        return @"//Pixel shader
float4 PSBasic(VSOutput pin) : SV_Target0
{
    return GetPixelDiffuseColor(pin);
}";
    }

    private string GetTechnique()
    {
        return @"//Technique declaration
TECHNIQUE(Effect_Compile, VSBasic, PSBasic);";
    }
}