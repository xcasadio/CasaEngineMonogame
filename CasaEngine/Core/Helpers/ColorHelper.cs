//-----------------------------------------------------------------------------
// ColorHelper.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

namespace CasaEngine.Core.Helpers;

/// <summary>
/// Color helper, just to convert colors to different formats and providing
/// more helper methods missing in the Color class.
/// </summary>
public static class ColorHelper
{
    /// <summary>
    /// Empty color, used to mark unused color values.
    /// </summary>
    public static readonly Color Empty = new(0, 0, 0, 0);

    /// <summary>
    /// Half alpha color helper. Just white with 50% alpha.
    /// </summary>
    public static readonly Color HalfAlpha = new(255, 255, 255, 128);

    /// <summary>
    /// Stay in range, val will be set to min if less or to max when bigger.
    /// </summary>
    private static float StayInRange(float val, float min, float max)
    {
        if (val < min)
        {
            return min;
        }

        if (val > max)
        {
            return max;
        }

        return val;
    }

    /// <summary>
    /// Multiply colors
    /// </summary>
    /// <param name="color1">Color 1</param>
    /// <param name="color2">Color 2</param>
    /// <returns>Return color</returns>
    public static Color MultiplyColors(Color color1, Color color2)
    {
        // Quick check if any of the colors is white,
        // multiplying won't do anything then.
        if (color1 == Color.White)
        {
            return color2;
        }

        if (color2 == Color.White)
        {
            return color1;
        }

        // Get values from color1
        float redValue1 = color1.R / 255.0f;
        float greenValue1 = color1.G / 255.0f;
        float blueValue1 = color1.B / 255.0f;
        float alphaValue1 = color1.A / 255.0f;

        // Get values from color2
        float redValue2 = color2.R / 255.0f;
        float greenValue2 = color2.G / 255.0f;
        float blueValue2 = color2.B / 255.0f;
        float alphaValue2 = color2.A / 255.0f;

        // Multiply everything using our floats
        return new Color(
            (byte)(StayInRange(redValue1 * redValue2, 0, 1) * 255.0f),
            (byte)(StayInRange(greenValue1 * greenValue2, 0, 1) * 255.0f),
            (byte)(StayInRange(blueValue1 * blueValue2, 0, 1) * 255.0f),
            (byte)(StayInRange(alphaValue1 * alphaValue2, 0, 1) * 255.0f));
    }

    /// <summary>
    /// Same color. Helper method for LoadLevel because for some reason
    /// the color compare does not work and causes a lot of errors.
    /// </summary>
    /// <param name="color">Color</param>
    /// <param name="checkColor">Check color</param>
    /// <returns>Bool</returns>
    public static bool SameColor(Color color, Color checkColor)
    {
        return color.R == checkColor.R &&
               color.G == checkColor.G &&
               color.B == checkColor.B;
    }

    /// <summary>
    /// Interpolate color. Used to fade the hud colors from green to red.
    /// </summary>
    public static Color InterpolateColor(Color col1, Color col2, float percent)
    {
        return new Color(
            (byte)(col1.R * (1.0f - percent) + col2.R * percent),
            (byte)(col1.G * (1.0f - percent) + col2.G * percent),
            (byte)(col1.B * (1.0f - percent) + col2.B * percent),
            (byte)(col1.A * (1.0f - percent) + col2.A * percent));
    }

    /// <summary>
    /// Apply alpha to color
    /// </summary>
    /// <param name="col">Color</param>
    /// <param name="newAlpha">New alpha</param>
    /// <returns>Color</returns>
    public static Color ApplyAlphaToColor(Color col, float newAlpha)
    {
        if (newAlpha < 0)
        {
            newAlpha = 0;
        }

        if (newAlpha > 1)
        {
            newAlpha = 1;
        }

        return new Color(
            col.R,
            col.G,
            col.B,
            (byte)(newAlpha * 255.0f));
    }

    /// <summary>
    /// Mix alpha to color
    /// </summary>
    /// <param name="col">Color</param>
    /// <param name="newAlpha">New alpha</param>
    /// <returns>Color</returns>
    public static Color MixAlphaToColor(Color col, float newAlpha)
    {
        if (newAlpha < 0)
        {
            newAlpha = 0;
        }

        if (newAlpha > 1)
        {
            newAlpha = 1;
        }

        return new Color(
            (byte)(col.R * newAlpha),
            (byte)(col.G * newAlpha),
            (byte)(col.B * newAlpha),
            (byte)(newAlpha * 255.0f));
    }
}