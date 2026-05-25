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
static const int MaxPointLights = 8;
static const int MaxSpotLights = 8;


float3 ComputeAmbientTerm(float3 globalAmbientColor, float3 materialAmbientColor)
{
    return globalAmbientColor * materialAmbientColor;
}


#ifdef HAS_ENVIRONMENT_BINDINGS
float3 ComputeEnvironmentDiffuseTerm(float3 globalEnvironmentAmbientColor, float3 materialAmbientColor)
{
    return globalEnvironmentAmbientColor * materialAmbientColor;
}


float3 SampleEnvironmentReflection(float3 reflectionVector)
{
    float3 globalReflection = 0;
    if (HasEnvironmentCubeTexture > 0.5f)
    {
        globalReflection = SAMPLE_CUBEMAP(EnvironmentCubeTexture, reflectionVector).rgb;
    }

    float3 localReflection = 0;
    float localWeightTotal = 0;

    if (HasLocalReflectionProbeTexture > 0.5f)
    {
        localReflection += SAMPLE_CUBEMAP(LocalReflectionProbeCubeTexture, reflectionVector).rgb * LocalReflectionProbeWeight;
        localWeightTotal += LocalReflectionProbeWeight;
    }

    if (HasSecondaryLocalReflectionProbeTexture > 0.5f)
    {
        localReflection += SAMPLE_CUBEMAP(SecondaryLocalReflectionProbeCubeTexture, reflectionVector).rgb * SecondaryLocalReflectionProbeWeight;
        localWeightTotal += SecondaryLocalReflectionProbeWeight;
    }

    if (localWeightTotal > 0.0f)
    {
        localReflection /= max(localWeightTotal, 0.0001f);
        globalReflection = lerp(globalReflection, localReflection, saturate(LocalReflectionProbeInfluence));
    }

    return globalReflection * EnvironmentSpecularIntensity;
}


float3 SampleEnvironmentDiffuse(float3 worldNormal)
{
    if (HasEnvironmentCubeTexture <= 0.5f)
    {
        return 0;
    }

    return SAMPLE_CUBEMAP(EnvironmentCubeTexture, normalize(worldNormal)).rgb;
}
#else
float3 ComputeEnvironmentDiffuseTerm(float3 globalEnvironmentAmbientColor, float3 materialAmbientColor)
{
    return globalEnvironmentAmbientColor * materialAmbientColor;
}


float3 SampleEnvironmentReflection(float3 reflectionVector)
{
    return 0;
}


float3 SampleEnvironmentDiffuse(float3 worldNormal)
{
    return 0;
}
#endif


#ifdef HAS_FORWARD_SHADOW_BINDINGS
float SampleDirectionalShadowPcf(float2 shadowUv, float shadowDepth)
{
    float visibility = 0.0f;

    visibility += step(shadowDepth, SAMPLE_TEXTURE_LEVEL(ShadowMapTexture, shadowUv + ShadowMapTexelSize * float2(-0.5f, -0.5f), 0.0f).r);
    visibility += step(shadowDepth, SAMPLE_TEXTURE_LEVEL(ShadowMapTexture, shadowUv + ShadowMapTexelSize * float2(0.5f, -0.5f), 0.0f).r);
    visibility += step(shadowDepth, SAMPLE_TEXTURE_LEVEL(ShadowMapTexture, shadowUv + ShadowMapTexelSize * float2(-0.5f, 0.5f), 0.0f).r);
    visibility += step(shadowDepth, SAMPLE_TEXTURE_LEVEL(ShadowMapTexture, shadowUv + ShadowMapTexelSize * float2(0.5f, 0.5f), 0.0f).r);

    return visibility * 0.25f;
}


float ComputeDirectionalShadowFactor(float3 worldPosition, float3 worldNormal)
{
    if (ActiveShadowLightCount <= 0.0f || ReceiveShadows <= 0.5f)
    {
        return 1.0f;
    }

    float3 biasedWorldPosition = worldPosition + worldNormal * ShadowNormalBias;
    float4 shadowClip = mul(float4(biasedWorldPosition, 1.0f), ShadowLightViewProjection);
    if (shadowClip.w <= 0.0f)
    {
        return 1.0f;
    }

    float3 shadowNdc = shadowClip.xyz / shadowClip.w;
    float2 shadowUv = float2(shadowNdc.x * 0.5f + 0.5f, -shadowNdc.y * 0.5f + 0.5f);
    if (shadowUv.x <= 0.0f || shadowUv.x >= 1.0f || shadowUv.y <= 0.0f || shadowUv.y >= 1.0f)
    {
        return 1.0f;
    }

    float shadowDepth = shadowNdc.z;

#if OPENGL
    shadowDepth = shadowDepth * 0.5f + 0.5f;
#endif

    shadowDepth -= ShadowDepthBias;
    if (shadowDepth <= 0.0f || shadowDepth >= 1.0f)
    {
        return 1.0f;
    }

    return SampleDirectionalShadowPcf(shadowUv, shadowDepth);
}
#else
float ComputeDirectionalShadowFactor(float3 worldPosition, float3 worldNormal)
{
    return 1.0f;
}
#endif


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


void AccumulateLight(
    inout ColorPair result,
    float3 eyeVector,
    float3 worldNormal,
    float3 surfaceToLight,
    float attenuation,
    float3 lightDiffuse,
    float3 lightSpecular)
{
    float3 halfVector = normalize(eyeVector + surfaceToLight);
    float dotL = dot(surfaceToLight, worldNormal);
    float zeroL = step(0, dotL);
    float diffuse = zeroL * dotL * attenuation;
    float specular = pow(max(dot(halfVector, worldNormal), 0) * zeroL, SpecularPower) * attenuation;

    result.Diffuse += diffuse * lightDiffuse * DiffuseColor.rgb;
    result.Specular += specular * lightSpecular * SpecularColor;
}


void AccumulatePointLightSlot(
    inout ColorPair result,
    float3 eyeVector,
    float3 worldPosition,
    float3 worldNormal,
    float4 lightPositionAndRange,
    float4 lightDiffuse,
    float4 lightSpecular)
{
    float3 toLight = lightPositionAndRange.xyz - worldPosition;
    float distanceToLight = length(toLight);
    float range = lightPositionAndRange.w;
    if (distanceToLight <= 0.0001f || range <= 0.0f)
    {
        return;
    }

    float attenuation = saturate(1.0f - distanceToLight / range);
    attenuation *= attenuation;
    if (attenuation <= 0.0f)
    {
        return;
    }

    AccumulateLight(result, eyeVector, worldNormal, toLight / distanceToLight, attenuation, lightDiffuse.xyz, lightSpecular.xyz);
}


void AccumulatePointLightSlots(inout ColorPair result, float3 eyeVector, float3 worldPosition, float3 worldNormal)
{
    int count = (int)ActivePointLightCount;
    if (count > 0) AccumulatePointLightSlot(result, eyeVector, worldPosition, worldNormal, PointLight0PositionAndRange, PointLight0DiffuseColor, PointLight0SpecularColor);
    if (count > 1) AccumulatePointLightSlot(result, eyeVector, worldPosition, worldNormal, PointLight1PositionAndRange, PointLight1DiffuseColor, PointLight1SpecularColor);
    if (count > 2) AccumulatePointLightSlot(result, eyeVector, worldPosition, worldNormal, PointLight2PositionAndRange, PointLight2DiffuseColor, PointLight2SpecularColor);
    if (count > 3) AccumulatePointLightSlot(result, eyeVector, worldPosition, worldNormal, PointLight3PositionAndRange, PointLight3DiffuseColor, PointLight3SpecularColor);
    if (count > 4) AccumulatePointLightSlot(result, eyeVector, worldPosition, worldNormal, PointLight4PositionAndRange, PointLight4DiffuseColor, PointLight4SpecularColor);
    if (count > 5) AccumulatePointLightSlot(result, eyeVector, worldPosition, worldNormal, PointLight5PositionAndRange, PointLight5DiffuseColor, PointLight5SpecularColor);
    if (count > 6) AccumulatePointLightSlot(result, eyeVector, worldPosition, worldNormal, PointLight6PositionAndRange, PointLight6DiffuseColor, PointLight6SpecularColor);
    if (count > 7) AccumulatePointLightSlot(result, eyeVector, worldPosition, worldNormal, PointLight7PositionAndRange, PointLight7DiffuseColor, PointLight7SpecularColor);
}


void AccumulateSpotLightSlot(
    inout ColorPair result,
    float3 eyeVector,
    float3 worldPosition,
    float3 worldNormal,
    float4 lightPositionAndRange,
    float4 directionAndInnerConeCos,
    float4 lightDiffuse,
    float4 specularAndOuterConeCos)
{
    float3 toLight = lightPositionAndRange.xyz - worldPosition;
    float distanceToLight = length(toLight);
    float range = lightPositionAndRange.w;
    if (distanceToLight <= 0.0001f || range <= 0.0f)
    {
        return;
    }

    float attenuation = saturate(1.0f - distanceToLight / range);
    attenuation *= attenuation;
    if (attenuation <= 0.0f)
    {
        return;
    }

    float3 surfaceToLight = toLight / distanceToLight;
    float spotCos = dot(directionAndInnerConeCos.xyz, -surfaceToLight);
    float coneAttenuation = saturate((spotCos - specularAndOuterConeCos.w) / max(directionAndInnerConeCos.w - specularAndOuterConeCos.w, 0.0001f));
    attenuation *= coneAttenuation;
    if (attenuation <= 0.0f)
    {
        return;
    }

    AccumulateLight(result, eyeVector, worldNormal, surfaceToLight, attenuation, lightDiffuse.xyz, specularAndOuterConeCos.xyz);
}


void AccumulateSpotLightSlots(inout ColorPair result, float3 eyeVector, float3 worldPosition, float3 worldNormal)
{
    int count = (int)ActiveSpotLightCount;
    if (count > 0) AccumulateSpotLightSlot(result, eyeVector, worldPosition, worldNormal, SpotLight0PositionAndRange, SpotLight0DirectionAndInnerConeCos, SpotLight0DiffuseColor, SpotLight0SpecularColorAndOuterConeCos);
    if (count > 1) AccumulateSpotLightSlot(result, eyeVector, worldPosition, worldNormal, SpotLight1PositionAndRange, SpotLight1DirectionAndInnerConeCos, SpotLight1DiffuseColor, SpotLight1SpecularColorAndOuterConeCos);
    if (count > 2) AccumulateSpotLightSlot(result, eyeVector, worldPosition, worldNormal, SpotLight2PositionAndRange, SpotLight2DirectionAndInnerConeCos, SpotLight2DiffuseColor, SpotLight2SpecularColorAndOuterConeCos);
    if (count > 3) AccumulateSpotLightSlot(result, eyeVector, worldPosition, worldNormal, SpotLight3PositionAndRange, SpotLight3DirectionAndInnerConeCos, SpotLight3DiffuseColor, SpotLight3SpecularColorAndOuterConeCos);
    if (count > 4) AccumulateSpotLightSlot(result, eyeVector, worldPosition, worldNormal, SpotLight4PositionAndRange, SpotLight4DirectionAndInnerConeCos, SpotLight4DiffuseColor, SpotLight4SpecularColorAndOuterConeCos);
    if (count > 5) AccumulateSpotLightSlot(result, eyeVector, worldPosition, worldNormal, SpotLight5PositionAndRange, SpotLight5DirectionAndInnerConeCos, SpotLight5DiffuseColor, SpotLight5SpecularColorAndOuterConeCos);
    if (count > 6) AccumulateSpotLightSlot(result, eyeVector, worldPosition, worldNormal, SpotLight6PositionAndRange, SpotLight6DirectionAndInnerConeCos, SpotLight6DiffuseColor, SpotLight6SpecularColorAndOuterConeCos);
    if (count > 7) AccumulateSpotLightSlot(result, eyeVector, worldPosition, worldNormal, SpotLight7PositionAndRange, SpotLight7DirectionAndInnerConeCos, SpotLight7DiffuseColor, SpotLight7SpecularColorAndOuterConeCos);
}


ColorPair ComputeLightsNoShadows(float3 eyeVector, float3 worldPosition, float3 worldNormal, uniform int numDirectionalLights)
{
    ColorPair result;
    result.Diffuse = EmissiveColor;
    result.Specular = 0;

    [unroll]
    for (int directionalIndex = 0; directionalIndex < MaxDirectionalLights; directionalIndex++)
    {
        if (directionalIndex >= numDirectionalLights)
        {
            break;
        }

        float3 lightDirection = GetDirectionalLightDirection(directionalIndex);
        float3 lightDiffuse = GetDirectionalLightDiffuseColor(directionalIndex);
        float3 lightSpecular = GetDirectionalLightSpecularColor(directionalIndex);
        AccumulateLight(result, eyeVector, worldNormal, -lightDirection, 1.0f, lightDiffuse, lightSpecular);
    }

    AccumulatePointLightSlots(result, eyeVector, worldPosition, worldNormal);
    AccumulateSpotLightSlots(result, eyeVector, worldPosition, worldNormal);

    return result;
}


ColorPair ComputeLights(float3 eyeVector, float3 worldPosition, float3 worldNormal, uniform int numDirectionalLights)
{
    ColorPair result;
    result.Diffuse = EmissiveColor;
    result.Specular = 0;

    [unroll]
    for (int i = 0; i < MaxDirectionalLights; i++)
    {
        if (i >= numDirectionalLights)
        {
            break;
        }

        float3 lightDirection = GetDirectionalLightDirection(i);
        float3 lightDiffuse = GetDirectionalLightDiffuseColor(i);
        float3 lightSpecular = GetDirectionalLightSpecularColor(i);
#ifdef HAS_FORWARD_SHADOW_BINDINGS
        float directionalShadowFactor = i == (int)ShadowedDirectionalLightIndex
            ? ComputeDirectionalShadowFactor(worldPosition, worldNormal)
            : 1.0f;
#else
        float directionalShadowFactor = 1.0f;
#endif
        AccumulateLight(result, eyeVector, worldNormal, -lightDirection, directionalShadowFactor, lightDiffuse, lightSpecular);
    }

    AccumulatePointLightSlots(result, eyeVector, worldPosition, worldNormal);
    AccumulateSpotLightSlots(result, eyeVector, worldPosition, worldNormal);

    return result;
}


CommonVSOutput ComputeCommonVSOutputWithLighting(float4 position, float3 normal, uniform int numLights)
{
    CommonVSOutput vout;
    
    float4 pos_ws = mul(position, World);
    float3 eyeVector = normalize(EyePosition - pos_ws.xyz);
    float3 worldNormal = normalize(mul(normal, WorldInverseTranspose));

    ColorPair lightResult = ComputeLightsNoShadows(eyeVector, pos_ws.xyz, worldNormal, numLights);
    
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