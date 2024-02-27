using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Primitives3D;

public class TeapotPrimitive : BezierPrimitive
{
    public TeapotPrimitive(float size = 1, int tessellation = 8)
    {
        if (tessellation < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(tessellation));
        }

        foreach (var patch in TeapotPatches)
        {
            // Because the teapot is symmetrical from left to right, we only store
            // data for one side, then tessellate each patch twice, mirroring in X.
            TessellatePatch(patch, tessellation, new Vector3(size, size, size));
            TessellatePatch(patch, tessellation, new Vector3(-size, size, size));

            if (patch.MirrorZ)
            {
                // Some parts of the teapot (the body, lid, and rim, but not the
                // handle or spout) are also symmetrical from front to back, so
                // we tessellate them four times, mirroring in Z as well as X.
                TessellatePatch(patch, tessellation, new Vector3(size, size, -size));
                TessellatePatch(patch, tessellation, new Vector3(-size, size, -size));
            }
        }
    }

    private void TessellatePatch(TeapotPatch patch, int tessellation, Vector3 scale)
    {
        var controlPoints = new Vector3[16];

        for (var i = 0; i < 16; i++)
        {
            var index = patch.Indices[i];
            controlPoints[i] = _teapotControlPoints[index] * scale;
        }

        var isMirrored = Math.Sign(scale.X) != Math.Sign(scale.Z);

        CreatePatchIndices(tessellation, isMirrored);
        CreatePatchVertices(controlPoints, tessellation, isMirrored);
    }

    private class TeapotPatch
    {
        public readonly int[] Indices;
        public readonly bool MirrorZ;

        public TeapotPatch(bool mirrorZ, int[] indices)
        {
            Debug.Assert(indices.Length == 16);

            Indices = indices;
            MirrorZ = mirrorZ;
        }
    }

    private static readonly TeapotPatch[] TeapotPatches =
    {
        // Rim.
        new(true, new[] { 102, 103, 104, 105, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }),
        // Body.
        new(true, new[] { 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27 }),
        new(true, new[] { 24, 25, 26, 27, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40 }),
        // Lid.
        new(true, new[] { 96, 96, 96, 96, 97, 98, 99, 100, 101, 101, 101, 101, 0, 1, 2, 3 }),
        new(true, new[] { 0, 1, 2, 3, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117 }),
        // Handle.
        new(false, new[] { 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56 }),
        new(false, new[] { 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 28, 65, 66, 67 }),
        // Spout.
        new(false, new[] { 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83 }),
        new(false, new[] { 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95 }),
        // Bottom.
        new(true, new[] { 118, 118, 118, 118, 124, 122, 119, 121, 123, 126, 125, 120, 40, 39, 38, 37 }),
    };

    private static readonly Vector3[] _teapotControlPoints =
    {
        new(0f, 0.345f, -0.05f),
        new(-0.028f, 0.345f, -0.05f),
        new(-0.05f, 0.345f, -0.028f),
        new(-0.05f, 0.345f, -0f),
        new(0f, 0.3028125f, -0.334375f),
        new(-0.18725f, 0.3028125f, -0.334375f),
        new(-0.334375f, 0.3028125f, -0.18725f),
        new(-0.334375f, 0.3028125f, -0f),
        new(0f, 0.3028125f, -0.359375f),
        new(-0.20125f, 0.3028125f, -0.359375f),
        new(-0.359375f, 0.3028125f, -0.20125f),
        new(-0.359375f, 0.3028125f, -0f),
        new(0f, 0.27f, -0.375f),
        new(-0.21f, 0.27f, -0.375f),
        new(-0.375f, 0.27f, -0.21f),
        new(-0.375f, 0.27f, -0f),
        new(0f, 0.13875f, -0.4375f),
        new(-0.245f, 0.13875f, -0.4375f),
        new(-0.4375f, 0.13875f, -0.245f),
        new(-0.4375f, 0.13875f, -0f),
        new(0f, 0.007499993f, -0.5f),
        new(-0.28f, 0.007499993f, -0.5f),
        new(-0.5f, 0.007499993f, -0.28f),
        new(-0.5f, 0.007499993f, -0f),
        new(0f, -0.105f, -0.5f),
        new(-0.28f, -0.105f, -0.5f),
        new(-0.5f, -0.105f, -0.28f),
        new(-0.5f, -0.105f, -0f),
        new(0f, -0.105f, 0.5f),
        new(0f, -0.2175f, -0.5f),
        new(-0.28f, -0.2175f, -0.5f),
        new(-0.5f, -0.2175f, -0.28f),
        new(-0.5f, -0.2175f, -0f),
        new(0f, -0.27375f, -0.375f),
        new(-0.21f, -0.27375f, -0.375f),
        new(-0.375f, -0.27375f, -0.21f),
        new(-0.375f, -0.27375f, -0f),
        new(0f, -0.2925f, -0.375f),
        new(-0.21f, -0.2925f, -0.375f),
        new(-0.375f, -0.2925f, -0.21f),
        new(-0.375f, -0.2925f, -0f),
        new(0f, 0.17625f, 0.4f),
        new(-0.075f, 0.17625f, 0.4f),
        new(-0.075f, 0.2325f, 0.375f),
        new(0f, 0.2325f, 0.375f),
        new(0f, 0.17625f, 0.575f),
        new(-0.075f, 0.17625f, 0.575f),
        new(-0.075f, 0.2325f, 0.625f),
        new(0f, 0.2325f, 0.625f),
        new(0f, 0.17625f, 0.675f),
        new(-0.075f, 0.17625f, 0.675f),
        new(-0.075f, 0.2325f, 0.75f),
        new(0f, 0.2325f, 0.75f),
        new(0f, 0.12f, 0.675f),
        new(-0.075f, 0.12f, 0.675f),
        new(-0.075f, 0.12f, 0.75f),
        new(0f, 0.12f, 0.75f),
        new(0f, 0.06375f, 0.675f),
        new(-0.075f, 0.06375f, 0.675f),
        new(-0.075f, 0.007499993f, 0.75f),
        new(0f, 0.007499993f, 0.75f),
        new(0f, -0.04875001f, 0.625f),
        new(-0.075f, -0.04875001f, 0.625f),
        new(-0.075f, -0.09562501f, 0.6625f),
        new(0f, -0.09562501f, 0.6625f),
        new(-0.075f, -0.105f, 0.5f),
        new(-0.075f, -0.18f, 0.475f),
        new(0f, -0.18f, 0.475f),
        new(0f, 0.02624997f, -0.425f),
        new(-0.165f, 0.02624997f, -0.425f),
        new(-0.165f, -0.18f, -0.425f),
        new(0f, -0.18f, -0.425f),
        new(0f, 0.02624997f, -0.65f),
        new(-0.165f, 0.02624997f, -0.65f),
        new(-0.165f, -0.12375f, -0.775f),
        new(0f, -0.12375f, -0.775f),
        new(0f, 0.195f, -0.575f),
        new(-0.0625f, 0.195f, -0.575f),
        new(-0.0625f, 0.17625f, -0.6f),
        new(0f, 0.17625f, -0.6f),
        new(0f, 0.27f, -0.675f),
        new(-0.0625f, 0.27f, -0.675f),
        new(-0.0625f, 0.27f, -0.825f),
        new(0f, 0.27f, -0.825f),
        new(0f, 0.28875f, -0.7f),
        new(-0.0625f, 0.28875f, -0.7f),
        new(-0.0625f, 0.2934375f, -0.88125f),
        new(0f, 0.2934375f, -0.88125f),
        new(0f, 0.28875f, -0.725f),
        new(-0.0375f, 0.28875f, -0.725f),
        new(-0.0375f, 0.298125f, -0.8625f),
        new(0f, 0.298125f, -0.8625f),
        new(0f, 0.27f, -0.7f),
        new(-0.0375f, 0.27f, -0.7f),
        new(-0.0375f, 0.27f, -0.8f),
        new(0f, 0.27f, -0.8f),
        new(0f, 0.4575f, -0f),
        new(0f, 0.4575f, -0.2f),
        new(-0.1125f, 0.4575f, -0.2f),
        new(-0.2f, 0.4575f, -0.1125f),
        new(-0.2f, 0.4575f, -0f),
        new(0f, 0.3825f, -0f),
        new(0f, 0.27f, -0.35f),
        new(-0.196f, 0.27f, -0.35f),
        new(-0.35f, 0.27f, -0.196f),
        new(-0.35f, 0.27f, -0f),
        new(0f, 0.3075f, -0.1f),
        new(-0.056f, 0.3075f, -0.1f),
        new(-0.1f, 0.3075f, -0.056f),
        new(-0.1f, 0.3075f, -0f),
        new(0f, 0.3075f, -0.325f),
        new(-0.182f, 0.3075f, -0.325f),
        new(-0.325f, 0.3075f, -0.182f),
        new(-0.325f, 0.3075f, -0f),
        new(0f, 0.27f, -0.325f),
        new(-0.182f, 0.27f, -0.325f),
        new(-0.325f, 0.27f, -0.182f),
        new(-0.325f, 0.27f, -0f),
        new(0f, -0.33f, -0f),
        new(-0.1995f, -0.33f, -0.35625f),
        new(0f, -0.31125f, -0.375f),
        new(0f, -0.33f, -0.35625f),
        new(-0.35625f, -0.33f, -0.1995f),
        new(-0.375f, -0.31125f, -0f),
        new(-0.35625f, -0.33f, -0f),
        new(-0.21f, -0.31125f, -0.375f),
        new(-0.375f, -0.31125f, -0.21f),
    };
}