#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0
#endif

// M A X   B O N E S 
#define MAX_BONES   180

// TEXTURES
sampler texSampler : register(s0)
{
    Texture = <TexDiffuse>;
    Filter = Anisotropic;
    AddressU = Mirror;
    AddressV = Mirror;
};
sampler normalSampler : register(s1)
{
    Texture = <TexNormalMap>;
    MinFilter = linear;
    MagFilter = linear;
    MipFilter = Anisotropic;
    AddressU = Mirror;
    AddressV = Mirror;
};
sampler specSampler : register(s2)
{
    Texture = <TexSpecular>;
    MinFilter = linear;
    MagFilter = linear;
    MipFilter = Anisotropic;
    AddressU = Mirror;
    AddressV = Mirror;
};

float4x4 World, WorldViewProj;
float3x3 WorldInverseTranspose;
float3 CamPos;

// MATERIAL & LIGHTING
float4 DiffuseColor;
float3 EmissiveColor; // Ambient factored into emissive already
float3 SpecularColor;
float SpecularPower;
float Shine_Amplify = 1;
float3 LightDir1;
float3 LightDiffCol1;
float3 LightSpecCol1;
float3 LightDir2;
float3 LightDiffCol2;
float3 LightSpecCol2;
float3 LightDir3;
float3 LightDiffCol3;
float3 LightSpecCol3;
float3 FogColor;
float4 FogVector;

matrix Bones[MAX_BONES];

// I N P U T S 
struct VS_In
{
    float4 pos : POSITION0;
//  float4 color        : COLOR0;      // currently not using (uncomment here and #defines in skinmodel & skin-loader to use color-per-vertex - and add color influence code in shaders)
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL0;
    float3 tangent : TANGENT0;
    float3 bitangent : BINORMAL0;
    uint4 indices : BLENDINDICES0;
    float4 weights : BLENDWEIGHT0;
};

// T R A N S F E R   F R O M   V E R T E X - S H A D E R   T O   P I X E L - S H A D E R 
// NORMAL_MAPPED__VS__OUTPUT
struct VS_N_Out                       // normal will be extracted from normalMap during pixel-shading, so no need to pass it
{
    float4 position : SV_POSITION;
//  float4 color      : COLOR0;       // currently not using (uncomment here and #defines in skinmodel & skin-loader to use color-per-vertex)
    float2 uv : TEXCOORD0;
    float4 worldPos : TEXCOORD1; // position in world space (we'll make the w component into the fog-factor)    
    float3x3 tanSpace : TEXCOORD2;
};
// REGULAR__VS__OUTPUT
struct VS_Out
{
    float4 position : SV_POSITION;
//  float4 color      : COLOR0;       // currently not using (uncomment here and #defines in skinmodel & skin-loader to use color-per-vertex)
    float2 uv : TEXCOORD0;
    float4 worldPos : TEXCOORD1; // position in world space (we'll make the w component into the fog-factor)    
    float3 normal : TEXCOORD2;
};




// V E R T E X    S H A D E R    T E C H N I Q U E S ----------------------------------------------------------------------------

// S K I N  -  calculates the position and normal from weighted bone matrices
void Skin(inout VS_In vin)
{
    float4x3 skinning = 0;
	[unroll]
    for (int i = 0; i < 4; i++)
    {
        skinning += Bones[vin.indices[i]] * vin.weights[i];
    } // looks up bone, uses bone matrix by some percentage (4 bones should add to 100%)
    vin.pos.xyz = mul(vin.pos, skinning);
    vin.normal = mul(vin.normal, (float3x3) skinning);
}

void SkinNorms(inout VS_In vin)
{
    float4x3 skinning = 0;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        skinning += Bones[vin.indices[i]] * vin.weights[i];
    } // looks up bone, uses bone matrix by some percentage (4 bones should add to 100%)
    vin.pos.xyz = mul(vin.pos, skinning);
    vin.normal = mul(vin.normal, (float3x3) skinning);
    vin.tangent = mul(vin.tangent, (float3x3) skinning);
    //vin.bitangent    = mul(vin.bitangent,(float3x3) skinning);
}



// V E R T E X   S H A D E R   3 L I G H T S   S K I N  ( & FOG )
VS_Out VertexShader_3Lights_Skin(VS_In vin)
{
    VS_Out vout;
    Skin(vin);

	// use dot-product of pos with FogVector to determine fog intensity (as done in Monogame's SkinnedEffect)
    float fogFactor = saturate(dot(vin.pos, FogVector)); // get intensity (clamp: 0-1) 

    float3 wpos = mul(vin.pos, World); // transform model to where it should be in world coordinates
    vout.normal = normalize(mul(vin.normal, WorldInverseTranspose)); // (using World-InverseTrans to prevent scaling & deformation issues for normals for skin animation) 
    vout.position = mul(vin.pos, WorldViewProj); // used by the shader-pipeline (to get to screen location) 
    vout.worldPos = float4(wpos, fogFactor); // send/interpolate fog factor in worldPos.w (piggyback) 
    vout.uv = vin.uv;
    return vout;
}



// V E R T E X  S H A D E R   3 L I G H T S   S K I N _ N O R M A L M A P P E D  ( & FOG )
// Technique made for 3 directional lights, normal-mapping, and skin
// Could interpolate 3 direction intensities based on relative distances to in-game lights (good enough for the look I'm going for - weaken other 2 lights for subtle cues when only 1 light in scene) 
// May want to consider 1 sun source and 1 dominant point-source (interpolated pos) to draw dynamic character shadows from (depthstencil shadows)
// or could also get into deferred lighting(great for many lights); or even get into PBR and/or ray-casting all the lighting(no need to shadow-map) if you're really feeling brave ^-^
VS_N_Out VertexShader_3Lights_Skin_Nmap(VS_In vin)
{
    VS_N_Out vout;
    SkinNorms(vin);

    vout.tanSpace[0] = normalize(mul(vin.tangent, WorldInverseTranspose));
    float3 n = normalize(mul(vin.normal, WorldInverseTranspose)).xyz;
    float3 t = vout.tanSpace[0];
    t = normalize(t - dot(t, n) * n); //orthogonalizes to improve quality	
    float3 b = normalize(cross(t, n));
    vout.tanSpace[1] = b;
    vout.tanSpace[2] = n;

    // use dot-product of pos with FogVector to determine fog intensity (as done in XNA/Monogame's SkinnedEffect)
    float fogFactor = saturate(dot(vin.pos, FogVector)); // get intensity (clamp: 0-1) 

    float3 wpos = mul(vin.pos, World); // transform model to where it should be in world coordinates
    vout.position = mul(vin.pos, WorldViewProj); // used by the shader-pipeline (to get to screen location) 
    vout.worldPos = float4(wpos, fogFactor); // send/interpolate fog factor in worldPos.w (piggyback) 
    vout.uv = vin.uv;
    return vout;
}





// P I X E L   S H A D E R   T E C H N I Q U E S ------------------------------------------------------------------------------------------------------------------------------------------
struct ColorPair
{
    float3 diffuse;
    float3 specular;
};
// C O M P U T E   L I G H T S 
ColorPair ComputeLights(float3 eyeVector, float3 normal, uniform int numLights)
{
    float3x3 lightDirections = 0, lightDiffuse = 0, lightSpecular = 0, halfVectors = 0;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        lightDirections[i] = float3x3(LightDir1, LightDir2, LightDir3)[i];
        lightDiffuse[i] = float3x3(LightDiffCol1, LightDiffCol2, LightDiffCol3)[i];
        lightSpecular[i] = float3x3(LightSpecCol1, LightSpecCol2, LightSpecCol3)[i];
        halfVectors[i] = normalize(eyeVector - lightDirections[i]);
    }
    float3 dotL = mul(-lightDirections, normal); // angle between light and surface (moreless)
    float3 dotH = mul(halfVectors, normal);
    float3 zeroL = step(float3(0, 0, 0), dotL); // clamp    
    float3 diffuse = zeroL * dotL;
    float3 specular = pow(max(dotH, 0) * zeroL, SpecularPower);
    ColorPair result;
    result.diffuse = mul(diffuse, lightDiffuse) * DiffuseColor.rgb + EmissiveColor; // diffuse-factor * texture color (+emissive color)
    result.specular = mul(specular, lightSpecular) * SpecularColor; // specular intensity * spec color
    return result;
}



// P I X E L  S H A D E R   3 L I G H T S _ S K I N ( & FOG - no normal-map )
// NOTE: this has been modified with the assumption that transparent stuff will be like glass or ice - super shiny (see below)
float4 PixelShader_3Lights_Skin(VS_Out pin) : SV_Target0
{
    float4 color = tex2D(texSampler, pin.uv) * DiffuseColor; // sample from the texture (& multiply by material's diffuse color [optional])    
    float3 eyeVector = normalize(CamPos - pin.worldPos.xyz); // this vector points from surface position toward camera
    float3 normal = normalize(pin.normal);
    
    ColorPair lit = ComputeLights(eyeVector, normal, 3);
        
    color.rgb *= lit.diffuse;
    
    if ((color.a < 0.8) && (lit.specular.r > 0.8))
    { // ADDED THIS TO MAKE EYES LOOK REALLY SHINY:    
        color.rgb += lit.specular;
        color.a = 1;
    }
    color.rgb += lit.specular * Shine_Amplify * ((1 - color.a) * 100); // <-- super-shiny control version (*100 adds a shine halo)
    //color.rgb += specular * color.a;                                 // <-- original version
    if (FogVector.w > 0)
        color.rgb = lerp(color.rgb, FogColor * color.a, pin.worldPos.w);
    return color;
}


// P I X E L  S H A D E R   3 L I G H T S   S K I N   N O R M A L M A P P E D  ( & FOG )
// NOTE: this has been modified with the assumption that transparent stuff will be like glass or ice - super shiny (see below)
float4 PixelShader_3Lights_Skin_Nmap(VS_N_Out pin) : SV_Target0
{
    float4 color = tex2D(texSampler, pin.uv) * DiffuseColor; // sample from the texture (& multiply by material's diffuse color [optional])    
    float3 eyeVector = normalize(CamPos - pin.worldPos.xyz); // this vector points from surface position toward camera
    float3 normCol = normalize(tex2D(normalSampler, pin.uv).xyz - float3(0.5f, 0.5f, 0.5f)); // get value from normal-map of range -1 to +1    
    float3 normal = normalize(mul(normCol, pin.tanSpace)); // get the normal value in tangent-space and ensure normalized length    

    ColorPair lit = ComputeLights(eyeVector, normal, 3);

    color.rgb *= lit.diffuse;

    if ((color.a < 0.8) && (lit.specular.r > 0.8))
    { // ADDED THIS TO MAKE EYES LOOK REALLY SHINY: 
        color.rgb += lit.specular;
        color.a = 1;
    }
    color.rgb += lit.specular * Shine_Amplify * ((1 - color.a) * 100); // <-- super-shiny control version (*100 adds a shine halo)   //color.rgb += lit.specular * color.a; // <-- original version

    if (FogVector.w > 0)
        color.rgb = lerp(color.rgb, FogColor * color.a, pin.worldPos.w);
    return color;
}



// T E C H N I Q U E S ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ 
#define TECHNIQUE(name, vsname, psname ) technique name { pass { VertexShader = compile VS_SHADERMODEL vsname(); PixelShader = compile PS_SHADERMODEL psname(); } }

TECHNIQUE(Skin_NormalMapped_Directional_Fog, VertexShader_3Lights_Skin_Nmap, PixelShader_3Lights_Skin_Nmap);  // NORMAL MAPS (& DIRECTIONAL LIGHTS & FOG)
TECHNIQUE(Skin_Directional_Fog, VertexShader_3Lights_Skin, PixelShader_3Lights_Skin);       // NO normal maps
