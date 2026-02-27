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


ColorPair ComputeLights(float3 eyeVector, float3 worldNormal, uniform int numLights)
{
    float3x3 lightDirections = 0;
    float3x3 lightDiffuse = 0;
    float3x3 lightSpecular = 0;
    float3x3 halfVectors = 0;
    
    [unroll]
    for (int i = 0; i < numLights; i++)
    {
        lightDirections[i] = DirLightDirections[i];
        lightDiffuse[i]    = DirLightDiffuseColors[i];
        lightSpecular[i]   = DirLightSpecularColors[i];
        
        halfVectors[i] = normalize(eyeVector - lightDirections[i]);
    }

    float3 dotL = mul(-lightDirections, worldNormal);
    float3 dotH = mul(halfVectors, worldNormal);
    
    float3 zeroL = step(0, dotL);

    float3 diffuse = zeroL * dotL;
    float3 specular = pow(max(dotH, 0) * zeroL, SpecularPower);

    ColorPair result;
    
    result.Diffuse = mul(diffuse, lightDiffuse) * DiffuseColor.rgb + EmissiveColor;
    result.Specular = mul(specular, lightSpecular) * SpecularColor;

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