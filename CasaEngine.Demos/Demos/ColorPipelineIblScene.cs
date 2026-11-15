using System;
using CasaEngine.Framework.Rendering.Environment;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The pure parts of the image-based-lighting measurement scene of <see cref="ColorPipelineDemo"/>
/// (<c>CASAENGINE_COLOR_DEMO_IBL=1</c>, plan T6.3): the procedural environment cubes and the CPU reference of the colour a
/// perfectly smooth metal sphere must show.
/// </summary>
internal static class ColorPipelineIblScene
{
    /// <summary>Side of a face of the procedural cube, in texels.</summary>
    public const int FaceSize = 64;

    /// <summary>The checkerboard has this many cells along each side of a face (8 x 8).</summary>
    public const int CellsPerSide = 8;

    private const int FaceCount = 6;

    private static readonly CubeMapFace[] Faces =
    {
        CubeMapFace.PositiveX, CubeMapFace.NegativeX, CubeMapFace.PositiveY,
        CubeMapFace.NegativeY, CubeMapFace.PositiveZ, CubeMapFace.NegativeZ,
    };

    /// <summary>The tint of the light cells of each face (+X, -X, +Y, -Y, +Z, -Z), display colours. The dark cells are 10 % of it.</summary>
    private static readonly Color[] FaceTints =
    {
        new(235, 120, 60), new(60, 120, 235), new(230, 230, 90),
        new(90, 170, 100), new(210, 70, 170), new(70, 210, 210),
    };

    /// <summary>
    /// The texels of the six faces (faces in the order +X, -X, +Y, -Y, +Z, -Z, rows from the top) of a cube whose faces carry an
    /// 8 x 8 checkerboard: the light cells have the tint of the face, the dark cells 10 % of it. <paramref name="rotation"/>
    /// shifts the tints between the faces, which gives distinct cubes for the environment changes.
    /// </summary>
    public static Color[][] BuildFacePixels(int rotation)
    {
        var faces = new Color[FaceCount][];
        int cell = FaceSize / CellsPerSide;
        for (int face = 0; face < FaceCount; face++)
        {
            Color tint = FaceTints[(face + rotation) % FaceCount];
            var dark = new Color((byte)(tint.R / 10), (byte)(tint.G / 10), (byte)(tint.B / 10));
            var pixels = new Color[FaceSize * FaceSize];
            for (int y = 0; y < FaceSize; y++)
            {
                for (int x = 0; x < FaceSize; x++)
                {
                    pixels[(y * FaceSize) + x] = (((x / cell) + (y / cell)) % 2 == 0) ? tint : dark;
                }
            }

            faces[face] = pixels;
        }

        return faces;
    }

    public static TextureCube CreateCube(GraphicsDevice graphicsDevice, Color[][] facePixels, string name)
    {
        var cube = new TextureCube(graphicsDevice, FaceSize, false, SurfaceFormat.Color) { Name = name };
        for (int face = 0; face < FaceCount; face++)
        {
            cube.SetData(Faces[face], facePixels[face]);
        }

        return cube;
    }

    /// <summary>
    /// The direction in which the camera ray through the centre of <paramref name="pixel"/> is reflected by the sphere
    /// (<paramref name="centre"/>, <paramref name="radius"/>); false when the ray misses it.
    /// </summary>
    public static bool TryComputeReflection(
        Matrix view, Matrix projection, int width, int height, Point pixel, Vector3 centre, float radius, out Vector3 reflection)
    {
        reflection = Vector3.Zero;
        var viewport = new Viewport(0, 0, width, height);
        Vector3 near = viewport.Unproject(new Vector3(pixel.X + 0.5f, pixel.Y + 0.5f, 0.0f), projection, view, Matrix.Identity);
        Vector3 far = viewport.Unproject(new Vector3(pixel.X + 0.5f, pixel.Y + 0.5f, 1.0f), projection, view, Matrix.Identity);
        Vector3 direction = Vector3.Normalize(far - near);

        Vector3 toOrigin = near - centre;
        float b = Vector3.Dot(toOrigin, direction);
        float c = Vector3.Dot(toOrigin, toOrigin) - (radius * radius);
        float discriminant = (b * b) - c;
        if (discriminant < 0.0f)
        {
            return false;
        }

        float distance = -b - MathF.Sqrt(discriminant);
        Vector3 hit = near + (direction * distance);
        Vector3 normal = Vector3.Normalize(hit - centre);
        reflection = Vector3.Reflect(direction, normal);
        return true;
    }

    /// <summary>The index of the face and the texel of the cube that <paramref name="direction"/> points at (the texel whose direction is the closest).</summary>
    public static (int Face, int X, int Y) FindTexel(Vector3 direction)
    {
        float best = float.MinValue;
        (int, int, int) result = (0, 0, 0);
        for (int face = 0; face < FaceCount; face++)
        {
            for (int y = 0; y < FaceSize; y++)
            {
                for (int x = 0; x < FaceSize; x++)
                {
                    float dot = Vector3.Dot(direction, IblCubeGenerator.GetTexelDirection(Faces[face], x, y, FaceSize));
                    if (dot > best)
                    {
                        best = dot;
                        result = (face, x, y);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// The colour the displayed image must show for a mirror that reflects <paramref name="direction"/>: the texel of the source cube,
    /// decoded to linear light in LinearHdr, multiplied by the specular intensity of the environment, passed through the same
    /// exposure, tonemapping operator and encoding as <c>Tonemap.fx</c>; in Legacy the texel times the intensity, as it is.
    /// </summary>
    public static Vector3 ReferenceColor(Color[][] facePixels, Vector3 direction, ColorPipelineMode mode, TonemapOperator tonemapper, float exposure, float specularIntensity)
    {
        (int face, int x, int y) = FindTexel(direction);
        Color texel = facePixels[face][(y * FaceSize) + x];
        Vector3 colour = texel.ToVector3();

        if (mode == ColorPipelineMode.Legacy)
        {
            return Vector3.Clamp(colour * specularIntensity, Vector3.Zero, Vector3.One);
        }

        var linear = new Vector3(MathF.Pow(colour.X, 2.2f), MathF.Pow(colour.Y, 2.2f), MathF.Pow(colour.Z, 2.2f));
        Vector3 c = Vector3.Max(linear * specularIntensity * exposure, Vector3.Zero);
        c = tonemapper == TonemapOperator.Reinhard
            ? new Vector3(c.X / (1.0f + c.X), c.Y / (1.0f + c.Y), c.Z / (1.0f + c.Z))
            : new Vector3(Aces(c.X), Aces(c.Y), Aces(c.Z));
        c = Vector3.Clamp(c, Vector3.Zero, Vector3.One);
        return new Vector3(MathF.Pow(c.X, 1.0f / 2.2f), MathF.Pow(c.Y, 1.0f / 2.2f), MathF.Pow(c.Z, 1.0f / 2.2f));
    }

    private static float Aces(float c) => (c * ((2.51f * c) + 0.03f)) / ((c * ((2.43f * c) + 0.59f)) + 0.14f);
}
