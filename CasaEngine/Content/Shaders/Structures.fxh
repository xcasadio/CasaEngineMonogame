//-----------------------------------------------------------------------------
// Structures.fxh
//
// Vertex/pixel shader input and output structures used by basicEffect.fx.
//-----------------------------------------------------------------------------


// Vertex shader input structures.

struct VSInput
{
    float4 Position : SV_Position;
};

struct VSInputVc
{
    float4 Position : SV_Position;
    float4 Color : COLOR;
};

struct VSInputTx
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

struct VSInputTxVc
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR;
};

struct VSInputNm
{
    float4 Position : SV_Position;
    float3 Normal : NORMAL;
};

struct VSInputNmVc
{
    float4 Position : SV_Position;
    float3 Normal : NORMAL;
    float4 Color : COLOR;
};

struct VSInputNmTx
{
    float4 Position : SV_Position;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
};

struct VSInputNmTxVc
{
    float4 Position : SV_Position;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR;
};

struct VSInputNmTxTan
{
    float4 Position : SV_Position;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD0;
    float4 Tangent : TANGENT;  // .w = bitangent sign
};


// Vertex shader output structures.

struct VSOutput
{
    float4 Diffuse : COLOR0;
    float4 Specular : COLOR1;
    float4 PositionPS : SV_Position;
};

struct VSOutputTx
{
    float4 Diffuse : COLOR0;
    float4 Specular : COLOR1;
    float2 TexCoord : TEXCOORD0;
    float4 PositionPS : SV_Position;
};

struct VSOutputPixelLighting
{
    float4 PositionWS : TEXCOORD0;
    float3 NormalWS : TEXCOORD1;
    float4 Diffuse : COLOR0;
    float4 PositionPS : SV_Position;
};

struct VSOutputPixelLightingTx
{
    float2 TexCoord : TEXCOORD0;
    float4 PositionWS : TEXCOORD1;
    float3 NormalWS : TEXCOORD2;
    float4 Diffuse : COLOR0;
    float4 PositionPS : SV_Position;
};

struct VSOutputPixelLightingTxTan
{
    float2 TexCoord : TEXCOORD0;
    float4 PositionWS : TEXCOORD1;
    float3 NormalWS : TEXCOORD2;
    float3 TangentWS : TEXCOORD3;
    float3 BitangentWS : TEXCOORD4;
    float4 Diffuse : COLOR0;
    float4 PositionPS : SV_Position;
};