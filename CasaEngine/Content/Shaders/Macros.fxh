//-----------------------------------------------------------------------------
// Macros.fxh
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

// Macros for targetting shader model 4.0 (DX11)
#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0
#endif

#define TECHNIQUE(name, vsname, psname ) \
	technique name { pass { VertexShader = compile VS_SHADERMODEL vsname (); PixelShader = compile PS_SHADERMODEL psname(); } }

#define BEGIN_CONSTANTS     cbuffer Parameters : register(b0) {
#define MATRIX_CONSTANTS
#define END_CONSTANTS       };

#define _vs(r)
#define _ps(r)
#define _cb(r)

#if OPENGL
#define DECLARE_TEXTURE(Name, index) \
    Texture2D Name; \
    sampler2D Name##Sampler = sampler_state { Texture = <Name>; }

#define DECLARE_CUBEMAP(Name, index) \
    TextureCube Name; \
    samplerCUBE Name##Sampler = sampler_state { Texture = <Name>; }
#else
#define DECLARE_TEXTURE(Name, index) \
    Texture2D<float4> Name : register(t##index); \
    sampler Name##Sampler : register(s##index)

#define DECLARE_CUBEMAP(Name, index) \
    TextureCube<float4> Name : register(t##index); \
    sampler Name##Sampler : register(s##index)
#endif

#if OPENGL
#define SAMPLE_TEXTURE(Name, texCoord)  tex2D(Name##Sampler, texCoord)
#define SAMPLE_TEXTURE_LEVEL(Name, texCoord, level)  tex2D(Name##Sampler, texCoord)
#define SAMPLE_CUBEMAP(Name, texCoord)  texCUBE(Name##Sampler, texCoord)
#else
#define SAMPLE_TEXTURE(Name, texCoord)  Name.Sample(Name##Sampler, texCoord)
#define SAMPLE_TEXTURE_LEVEL(Name, texCoord, level)  Name.SampleLevel(Name##Sampler, texCoord, level)
#define SAMPLE_CUBEMAP(Name, texCoord)  Name.Sample(Name##Sampler, texCoord)
#endif
