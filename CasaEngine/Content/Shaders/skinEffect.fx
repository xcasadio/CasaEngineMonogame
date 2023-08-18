// for reference
// struct semantics
//float2 TexureCoordinate : TEXCOORD0; //float4 Color : COLOR0; //float3 Normal : NORMAL0; //float3 Tangent : NORMAL1; //float4 boneIds : BLENDINDICES; //float4 boneWeights : BLENDWEIGHT;
// sampler options
//magfilter = LINEAR; //minfilter = LINEAR; //mipfilter = LINEAR; //AddressU = mirror; //AddressV = mirror; 

//_______________________________________________________________
// RiggedModelEffect.fx
// Simple texture drawing Effects are also included.
//
// Techniques in this fx file...
//
// RiggedModelDraw, SkinedDebugModelDraw, ColorTextureLightingDraw,
// TextureDraw,  ColorTextureDraw, ColorTextureDraw
//
// Defines
//_______________________________________________________________

#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0
#endif



//_______________________________________________________________
// textures and samplers
//
//_______________________________________________________________


Texture2D TextureA; // primary texture.
sampler TextureSamplerA = sampler_state
{
    texture = <TextureA>;
};



//_______________________________________________________________
// members
//
//_______________________________________________________________

matrix World;
matrix View;
matrix Projection;
matrix Bones[128];

float3 WorldLightPosition;
float4 LightColor;
float3 CameraPosition;
float AmbientAmt = 0.1f;
float DiffuseAmt = 0.7f;
float SpecularAmt = 0.2f;
float SpecularSharpness = 0.5f;
float SpecularLightVsTexelInfluence = 0.5f;

float boneIdToSee = -1.0f; // more of a debuging value then anything



//_______________________________________________________________
// structs
// used by:  RiggedModelDraw, SkinedDebugModelDraw
//_______________________________________________________________
struct VsInputSkinnedQuad
{
    float3 Position : POSITION0;
    float2 TexureCoordinateA : TEXCOORD0;
    float3 Normal : NORMAL0;
    //float3 Tangent : NORMAL1;
    //float3 BiTangent : NORMAL2;
    float4 BlendIndices : BLENDINDICES0;
    float4 BlendWeights : BLENDWEIGHT0;
};
struct VsOutputSkinnedQuad
{
    float4 Position : SV_Position;
    float4 Color : Color0;
    float2 TexureCoordinateA : TEXCOORD0;
    float3 Position3D : TEXCOORD1;
    float3 Normal3D : TEXCOORD2;
    //float3 Tangent3D : NORMAL1;
    //float3 BiTangent3D : NORMAL2;
};



//_______________________________________________________________
// techniques 
// RiggedModelDraw
//
// This technique is used to deform a rigged model meshes via its bone transforms.
//
//_______________________________________________________________

VsOutputSkinnedQuad VertexShaderRiggedModelDraw(VsInputSkinnedQuad input)
{
    VsOutputSkinnedQuad output;

    float4 pos = float4(input.Position, 1);
    float3 norm = input.Normal;

    //pos = mul(pos, World);

    float sum = input.BlendWeights.x + input.BlendWeights.y + input.BlendWeights.z + input.BlendWeights.w;
    float4x4 mbones =
    Bones[input.BlendIndices.x] * (float) input.BlendWeights.x / sum +
    Bones[input.BlendIndices.y] * (float) input.BlendWeights.y / sum +
    Bones[input.BlendIndices.z] * (float) input.BlendWeights.z / sum +
    Bones[input.BlendIndices.w] * (float) input.BlendWeights.w / sum;
    pos = mul(pos, mbones);
    norm = mul(norm, mbones);
    
    pos = mul(pos, World);
    norm = normalize(mul(norm, World));

    output.Color = float4(1.0f, 1.0f, 1.0f, 1.0f); // place holder dunno if added color.
    output.TexureCoordinateA = input.TexureCoordinateA;
    output.Position3D = pos.xyz;
    output.Normal3D = norm;
    float4x4 vp = mul(View, Projection);
    output.Position = mul(pos, vp);
    return output;
}

float4 PixelShaderRiggedModelDraw(VsOutputSkinnedQuad input) : COLOR0
{
    float3 N = input.Normal3D;
    float3 L = normalize(WorldLightPosition - input.Position3D);
    float3 C = normalize(CameraPosition - input.Position3D); //View._m30_m31_m32
    float diffuse = saturate(dot(N, L)) * DiffuseAmt;
    float reflectionTheta = dot(C, reflect(-L, N));
    float IsFrontFace = sign(saturate(dot(L, N))); // 1 is Frontface 0 is Backface.
    float4 texelColor = tex2D(TextureSamplerA, input.TexureCoordinateA) * input.Color;
    float specular = saturate(reflectionTheta - SpecularSharpness) * (1.0f / (1.0f - SpecularSharpness)) * IsFrontFace * SpecularAmt; // screw that phong shading and power its nice but it also sucks.
    float4 result = (texelColor * AmbientAmt) + (texelColor * diffuse) + ((texelColor * (1.0f - SpecularLightVsTexelInfluence) + LightColor * SpecularLightVsTexelInfluence) * specular);
    return tex2D(TextureSamplerA, input.TexureCoordinateA);
}

technique RiggedModelDraw
{
    pass
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderRiggedModelDraw();
        PixelShader = compile PS_SHADERMODEL PixelShaderRiggedModelDraw();
    }
}

//_______________________________________________________________
// techniques 
// RiggedModelNormalDraw
//
// This technique was designed to display model normals extending from vertices
// The normals themselves are user primitives derived from a rigged models vertices.
//_______________________________________________________________

float4 PixelShaderRiggedModelNormalDraw(VsOutputSkinnedQuad input) : COLOR0
{
    float3 N = input.Normal3D;
    float3 L = normalize(WorldLightPosition - input.Position3D);
    float3 C = normalize(CameraPosition - input.Position3D); //View._m30_m31_m32
    float diffuse = saturate(dot(N, L));
    //float reflectionTheta = dot(C, reflect(-L, N));
    float IsFrontFace = sign(saturate(dot(L, N))); // 1 is Frontface 0 is Backface.
    float4 texelColor = tex2D(TextureSamplerA, input.TexureCoordinateA) * input.Color;
    //float specular = saturate(reflectionTheta - specularSharpness) * (1.0f / (1.0f - specularSharpness)) * IsFrontFace; // screw that phong shading and power its nice but it also sucks.
    float4 lightColor = float4(0.99f, .99f, 0.99f, 1.0f) * IsFrontFace + float4(0.99f, 0.09f, 0.09f, 1.0f) * (1.0f - IsFrontFace);
    float4 result = (lightColor * 0.60f + texelColor * 0.40f) * (diffuse * 0.75f + 0.25f);
    return tex2D(TextureSamplerA, input.TexureCoordinateA);
}

technique RiggedModelNormalDraw
{
    pass
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderRiggedModelDraw(); //VertexShaderRiggedModelNormalDraw();
        PixelShader = compile PS_SHADERMODEL PixelShaderRiggedModelNormalDraw();
    }
}

//_______________________________________________________________
// techniques 
// SkinedDebugModelDraw
//
//  This is a model draw using bones. 
//  it is designed to display a selected bones vertices.
//
//_______________________________________________________________

VsOutputSkinnedQuad VertexShaderDebugSkinnedDraw(VsInputSkinnedQuad input)
{
    VsOutputSkinnedQuad output;

    float4 pos = float4(input.Position, 1);
    float3 norm = input.Normal;

    //pos = mul(pos, World);

    float sum = input.BlendWeights.x + input.BlendWeights.y + input.BlendWeights.z + input.BlendWeights.w;
    float4x4 mbones =
    Bones[input.BlendIndices.x] * (float) input.BlendWeights.x / sum +
    Bones[input.BlendIndices.y] * (float) input.BlendWeights.y / sum +
    Bones[input.BlendIndices.z] * (float) input.BlendWeights.z / sum +
    Bones[input.BlendIndices.w] * (float) input.BlendWeights.w / sum;
    pos = mul(pos, mbones);
    norm = mul(norm, mbones);
    
    pos = mul(pos, World);
    norm = normalize(mul(norm, World));

    float4 col = float4(0.40f, 0.40f, 0.40f, 1.0f);

    // i could get rid of the if with some math but its just a debug shader.
    if (input.BlendIndices.x == boneIdToSee || input.BlendIndices.y == boneIdToSee || input.BlendIndices.z == boneIdToSee || input.BlendIndices.w == boneIdToSee)
        col = float4(0.49f, 0.99f, 0.49f, 1.0f);

    output.Color = col;
    output.TexureCoordinateA = input.TexureCoordinateA;
    output.Position3D = pos.xyz;
    output.Normal3D = norm;
    float4x4 vp = mul(View, Projection);
    output.Position = mul(pos, vp);
    return output;
}

float4 PixelShaderDebugSkinnedDraw(VsOutputSkinnedQuad input) : COLOR0
{
    float4 result = tex2D(TextureSamplerA, input.TexureCoordinateA) * input.Color;
    return result;
}

technique SkinedDebugModelDraw
{
    pass
    {
        VertexShader = compile VS_SHADERMODEL VertexShaderDebugSkinnedDraw();
        PixelShader = compile PS_SHADERMODEL PixelShaderDebugSkinnedDraw();
    }
}


