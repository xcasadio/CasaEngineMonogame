//-----------------------------------------------------------------------------
// ColorPipeline.fxh
//
// Display-colour decoding of the linear colour pipeline (ADR-0105).
//
// The including effect declares `float ColorPipelineIsLinear;` BEFORE including this file. The engine binds it to 1 only
// for a view that really draws through the HDR scene target in this frame, and to 0 otherwise. With 0 every function here
// returns its input untouched, so the legacy image is bit-identical.
//
// The decoding is the 2.2 power law, the inverse of the pow(c, 1 / 2.2) that Tonemap.fx writes. It is never applied to
// data that is not a colour (normal maps, the shadow map) nor to the detail sample alone: the base colour is decoded after
// its modulation by the detail texture.
//-----------------------------------------------------------------------------

#ifndef COLOR_PIPELINE_FXH
#define COLOR_PIPELINE_FXH

float3 DecodeColor(float3 c)
{
    // A select, not a branch: with the constant at 0 the value is returned untouched, bit for bit (pow(c, 1) is not exact).
    return ColorPipelineIsLinear > 0.5f ? pow(abs(c), 2.2f) : c;
}

// Decodes the colour channels of a colour with alpha; the alpha channel is left alone.
float4 DecodeColor4(float4 c)
{
    c.rgb = DecodeColor(c.rgb);
    return c;
}

#endif
