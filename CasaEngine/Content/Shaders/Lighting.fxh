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

    visibility += step(shadowDepth, ShadowMapTexture.SampleLevel(ShadowMapTextureSampler, shadowUv + ShadowMapTexelSize * float2(-0.5f, -0.5f), 0.0f).r);
    visibility += step(shadowDepth, ShadowMapTexture.SampleLevel(ShadowMapTextureSampler, shadowUv + ShadowMapTexelSize * float2(0.5f, -0.5f), 0.0f).r);
    visibility += step(shadowDepth, ShadowMapTexture.SampleLevel(ShadowMapTextureSampler, shadowUv + ShadowMapTexelSize * float2(-0.5f, 0.5f), 0.0f).r);
    visibility += step(shadowDepth, ShadowMapTexture.SampleLevel(ShadowMapTextureSampler, shadowUv + ShadowMapTexelSize * float2(0.5f, 0.5f), 0.0f).r);

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

    int numPointLights = (int)ActivePointLightCount;
    [unroll]
    for (int i = 0; i < MaxPointLights; i++)
    {
        if (i >= numPointLights)
        {
            break;
        }

        float4 lightPositionAndRange = PointLightPositionAndRange[i];
        float3 toLight = lightPositionAndRange.xyz - worldPosition;
        float distanceToLight = length(toLight);
        float range = lightPositionAndRange.w;
        if (distanceToLight <= 0.0001f || range <= 0.0f)
        {
            continue;
        }

        float attenuation = saturate(1.0f - distanceToLight / range);
        attenuation *= attenuation;
        if (attenuation <= 0.0f)
        {
            continue;
        }

        float3 surfaceToLight = toLight / distanceToLight;
        AccumulateLight(
            result,
            eyeVector,
            worldNormal,
            surfaceToLight,
            attenuation,
            PointLightDiffuseColors[i].xyz,
            PointLightSpecularColors[i].xyz);
    }

    int numSpotLights = (int)ActiveSpotLightCount;
    [unroll]
    for (int i = 0; i < MaxSpotLights; i++)
    {
        if (i >= numSpotLights)
        {
            break;
        }

        float4 lightPositionAndRange = SpotLightPositionAndRange[i];
        float3 toLight = lightPositionAndRange.xyz - worldPosition;
        float distanceToLight = length(toLight);
        float range = lightPositionAndRange.w;
        if (distanceToLight <= 0.0001f || range <= 0.0f)
        {
            continue;
        }

        float attenuation = saturate(1.0f - distanceToLight / range);
        attenuation *= attenuation;
        if (attenuation <= 0.0f)
        {
            continue;
        }

        float3 surfaceToLight = toLight / distanceToLight;
        float4 directionAndInnerConeCos = SpotLightDirectionAndInnerConeCos[i];
        float4 specularAndOuterConeCos = SpotLightSpecularColorsAndOuterConeCos[i];
        float spotCos = dot(directionAndInnerConeCos.xyz, -surfaceToLight);
        float innerConeCos = directionAndInnerConeCos.w;
        float outerConeCos = specularAndOuterConeCos.w;
        float coneAttenuation = saturate((spotCos - outerConeCos) / max(innerConeCos - outerConeCos, 0.0001f));
        attenuation *= coneAttenuation;
        if (attenuation <= 0.0f)
        {
            continue;
        }

        AccumulateLight(
            result,
            eyeVector,
            worldNormal,
            surfaceToLight,
            attenuation,
            SpotLightDiffuseColors[i].xyz,
            specularAndOuterConeCos.xyz);
    }

    return result;
}


CommonVSOutput ComputeCommonVSOutputWithLighting(float4 position, float3 normal, uniform int numLights)
{
    CommonVSOutput vout;
    
    float4 pos_ws = mul(position, World);
    float3 eyeVector = normalize(EyePosition - pos_ws.xyz);
    float3 worldNormal = normalize(mul(normal, WorldInverseTranspose));

    ColorPair lightResult = ComputeLights(eyeVector, pos_ws.xyz, worldNormal, numLights);
    
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