//-----------------------------------------------------------------------------
// Lighting.fxh
//
// Shared lighting functions and common VS helpers.
// Originally split across Common.fxh + Lighting.fxh; merged for simplicity.
//-----------------------------------------------------------------------------


void AddSpecular(inout float4 color, float3 specular)
{
    color.rgb += specular * color.a;
}


struct CommonVSOutput
{
    float4 Pos_ps;
    float4 Diffuse;
    float3 Specular;
};


CommonVSOutput ComputeCommonVSOutput(float4 position)
{
    CommonVSOutput vout;
    
    vout.Pos_ps = mul(position, WorldViewProj);
    vout.Diffuse = DiffuseColor;
    vout.Specular = 0;
    
    return vout;
}


#define SetCommonVSOutputParams \
    vout.PositionPS = cout.Pos_ps; \
    vout.Diffuse = cout.Diffuse; \
    vout.Specular = float4(cout.Specular, 1);


struct ColorPair
{
    float3 Diffuse;
    float3 Specular;
};


static const int MaxDirectionalLights = 8;


float3 ComputeAmbientTerm(float3 globalAmbientColor, float3 materialAmbientColor)
{
    return globalAmbientColor * materialAmbientColor;
}


float3 ComposeLitSurfaceColor(float3 albedo, float3 directDiffuse, float3 ambientTerm, float3 emissiveColor)
{
    return albedo * (directDiffuse + ambientTerm) + emissiveColor;
}


float3 GetDirectionalLightDirection(int index)
{
    if (index == 0) return DirLight0Direction;
    if (index == 1) return DirLight1Direction;
    if (index == 2) return DirLight2Direction;
    if (index == 3) return DirLight3Direction;
    if (index == 4) return DirLight4Direction;
    if (index == 5) return DirLight5Direction;
    if (index == 6) return DirLight6Direction;
    if (index == 7) return DirLight7Direction;
    return 0;
}


float3 GetDirectionalLightDiffuseColor(int index)
{
    if (index == 0) return DirLight0DiffuseColor;
    if (index == 1) return DirLight1DiffuseColor;
    if (index == 2) return DirLight2DiffuseColor;
    if (index == 3) return DirLight3DiffuseColor;
    if (index == 4) return DirLight4DiffuseColor;
    if (index == 5) return DirLight5DiffuseColor;
    if (index == 6) return DirLight6DiffuseColor;
    if (index == 7) return DirLight7DiffuseColor;
    return 0;
}


float3 GetDirectionalLightSpecularColor(int index)
{
    if (index == 0) return DirLight0SpecularColor;
    if (index == 1) return DirLight1SpecularColor;
    if (index == 2) return DirLight2SpecularColor;
    if (index == 3) return DirLight3SpecularColor;
    if (index == 4) return DirLight4SpecularColor;
    if (index == 5) return DirLight5SpecularColor;
    if (index == 6) return DirLight6SpecularColor;
    if (index == 7) return DirLight7SpecularColor;
    return 0;
}


ColorPair ComputeLights(float3 eyeVector, float3 worldNormal, uniform int numLights)
{
    ColorPair result;
    result.Diffuse = EmissiveColor;
    result.Specular = 0;

    [unroll]
    for (int i = 0; i < MaxDirectionalLights; i++)
    {
        if (i >= numLights)
        {
            break;
        }

        float3 lightDirection = GetDirectionalLightDirection(i);
        float3 lightDiffuse = GetDirectionalLightDiffuseColor(i);
        float3 lightSpecular = GetDirectionalLightSpecularColor(i);
        float3 halfVector = normalize(eyeVector - lightDirection);

        float dotL = dot(-lightDirection, worldNormal);
        float zeroL = step(0, dotL);
        float diffuse = zeroL * dotL;
        float specular = pow(max(dot(halfVector, worldNormal), 0) * zeroL, SpecularPower);

        result.Diffuse += diffuse * lightDiffuse * DiffuseColor.rgb;
        result.Specular += specular * lightSpecular * SpecularColor;
    }

    return result;
}


CommonVSOutput ComputeCommonVSOutputWithLighting(float4 position, float3 normal, uniform int numLights)
{
    CommonVSOutput vout;
    
    float4 pos_ws = mul(position, World);
    float3 eyeVector = normalize(EyePosition - pos_ws.xyz);
    float3 worldNormal = normalize(mul(normal, WorldInverseTranspose));

    ColorPair lightResult = ComputeLights(eyeVector, worldNormal, numLights);
    
    vout.Pos_ps = mul(position, WorldViewProj);
    vout.Diffuse = float4(lightResult.Diffuse, DiffuseColor.a);
    vout.Specular = lightResult.Specular;
    
    return vout;
}


struct CommonVSOutputPixelLighting
{
    float4 Pos_ps;
    float3 Pos_ws;
    float3 Normal_ws;
};


CommonVSOutputPixelLighting ComputeCommonVSOutputPixelLighting(float4 position, float3 normal)
{
    CommonVSOutputPixelLighting vout;
    
    vout.Pos_ps = mul(position, WorldViewProj);
    vout.Pos_ws = mul(position, World).xyz;
    vout.Normal_ws = normalize(mul(normal, WorldInverseTranspose));
    
    return vout;
}


#define SetCommonVSOutputParamsPixelLighting \
    vout.PositionPS = cout.Pos_ps; \
    vout.PositionWS = float4(cout.Pos_ws, 1); \
    vout.NormalWS = cout.Normal_ws;