using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests.UI.Backend;
using CasaEngine.Tests.Video;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The oriented sprite on a real device (ADR-0151, ADR-0152): two pictures that are children of a tilted camera standing
/// far from the origin of the world, and an opaque sprite of the world between them, drawn through
/// <c>World.Draw</c> and read back. The near picture has a cut-out; the depth decides what is seen.
/// </summary>
public sealed class OrientedSpriteGpuTests
{
    private const int TargetSize = 64;
    private const int TexelPixels = 16;
    private const float FarDistance = 200f;
    private const float ObjectDistance = 150f;
    private const float NearDistance = 100f;

    private static readonly Color FarColor = new(200, 30, 30);
    private static readonly Color NearColor = new(30, 200, 30);
    private static readonly Color ObjectColor = new(30, 30, 220);

    [GpuFact]
    public void TheDepthDecides_BetweenTwoPicturesOfATiltedCamera_AndAnObjectBetweenThem()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            byte[] pixels = Render(Vector2.Zero);

            //  The cut-out of the near picture is the centre of the frame, pixels 16 to 48; the object covers the right
            //  half of the frame.
            AssertClose(NearColor, PixelAt(pixels, 8, 8), "the near picture outside its cut-out");
            AssertClose(NearColor, PixelAt(pixels, 56, 56), "the near picture in front of the object");
            AssertClose(FarColor, PixelAt(pixels, 24, 32), "the far picture through the cut-out");
            AssertClose(ObjectColor, PixelAt(pixels, 40, 32), "the object through the cut-out, in front of the far picture");
        });
    }

    [GpuFact]
    public void ThePictureOffset_ShiftsThePicturesAndTheObjectTogether()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            //  A quarter of the width to the right: 16 pixels.
            byte[] pixels = Render(new Vector2(0.25f, 0f));

            AssertClose(NearColor, PixelAt(pixels, 24, 32), "the near picture, whose cut-out now starts at pixel 32");
            AssertClose(FarColor, PixelAt(pixels, 40, 32), "the far picture through the cut-out");
            AssertClose(ObjectColor, PixelAt(pixels, 56, 32), "the object through the cut-out");
            AssertClose(NearColor, PixelAt(pixels, 8, 8), "the near picture, larger than the frame, still covers the left edge");
        });
    }

    private static byte[] Render(Vector2 pictureOffset)
    {
        GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
        var harness = new WorldVideoHarness(null, device);
        harness.PrepareRendering(GpuDeviceHost.Instance.Game);

        //  A camera far from the origin of the world, with yaw, pitch and roll. It joins no world, so its viewport is set here.
        var camera = new PerspectiveCameraComponent
        {
            LocalPosition = new Vector3(5000f, 800f, 3000f),
            LocalOrientation = Quaternion.CreateFromYawPitchRoll(0.5f, -0.4f, 0.3f),
        };
        camera.FarPlane = 1000f;
        camera.NearPlane = 1f;
        camera.OnScreenResized(TargetSize, TargetSize);
        camera.PictureOffset = pictureOffset;

        //  Two pictures of 8 x 8 texels, 16 pixels per texel: twice the frame in each direction. The near one has a
        //  cut-out of 2 x 2 texels at its centre.
        using Texture2D farTexture = CreateTexture(device, 8, (_, _) => FarColor);
        using Texture2D nearTexture = CreateTexture(device, 8, (x, y) => x is 3 or 4 && y is 3 or 4 ? Color.Transparent : NearColor);
        using Texture2D objectTexture = CreateTexture(device, 1, (_, _) => ObjectColor);

        var farPicture = CreateSprite(harness.Renderer, farTexture, 8);
        farPicture.LocalPosition = new Vector3(0f, 0f, -FarDistance);
        farPicture.LocalScale = new Vector3(TexelPixels * UnitsPerPixel(camera, FarDistance), TexelPixels * UnitsPerPixel(camera, FarDistance), 1f);
        var nearPicture = CreateSprite(harness.Renderer, nearTexture, 8);
        nearPicture.LocalPosition = new Vector3(0f, 0f, -NearDistance);
        nearPicture.LocalScale = new Vector3(TexelPixels * UnitsPerPixel(camera, NearDistance), TexelPixels * UnitsPerPixel(camera, NearDistance), 1f);
        camera.AddChildComponent(farPicture);
        camera.AddChildComponent(nearPicture);
        var cameraEntity = new Entity { Name = "camera", RootComponent = camera };

        //  A sprite of the world, not a child of the camera: placed by its own transform between the two pictures, on the
        //  right half of the frame, turned like the camera. Its origin is its top left corner, put at the top of the
        //  frame, on its vertical centre line: 32 pixels above the centre.
        float objectUnit = UnitsPerPixel(camera, ObjectDistance);
        var worldObject = CreateSprite(harness.Renderer, objectTexture, 1);
        worldObject.LocalOrientation = camera.LocalOrientation;
        worldObject.LocalPosition = Vector3.Transform(new Vector3(0f, 32f * objectUnit, -ObjectDistance), camera.WorldMatrixNoScale);
        worldObject.LocalScale = new Vector3(32f * objectUnit, 64f * objectUnit, 1f);
        var objectEntity = new Entity { Name = "object", RootComponent = worldObject };

        //  The world draws what its index returns for the frustum of the frame: the origin of the world is outside it.
        var frame = RenderFrameFactory.From(camera, new Rectangle(0, 0, TargetSize, TargetSize));
        Assert.Equal(ContainmentType.Disjoint, new BoundingFrustum(frame.ViewProjection).Contains(Vector3.Zero));
        harness.World.SpatialServices.WorldIndex.Add(cameraEntity, cameraEntity.GetBoundingBox());
        harness.World.SpatialServices.WorldIndex.Add(objectEntity, objectEntity.GetBoundingBox());

        using var target = new RenderTarget2D(device, TargetSize, TargetSize, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        var previousTargets = device.GetRenderTargets();
        device.SetRenderTarget(target);
        device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1f, 0);

        harness.World.Draw(in frame);
        Assert.Equal(3, harness.Queue.Count);
        harness.Renderer.Flush(frame);

        device.SetRenderTargets(previousTargets);
        var pixels = new byte[TargetSize * TargetSize * 4];
        target.GetData(pixels);
        return pixels;
    }

    //  World units covered by one pixel of the frame, at a distance of the camera.
    private static float UnitsPerPixel(PerspectiveCameraComponent camera, float distance)
    {
        return 2f * distance * MathF.Tan(camera.FieldOfView * 0.5f) / TargetSize;
    }

    private static Texture2D CreateTexture(GraphicsDevice device, int size, Func<int, int, Color> texel)
    {
        var texture = new Texture2D(device, size, size, false, SurfaceFormat.Color);
        var data = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                data[y * size + x] = texel(x, y);
            }
        }

        texture.SetData(data);
        return texture;
    }

    //  A static sprite with its picture and its renderer set by hand: no asset catalogue, no world.
    private static StaticSpriteComponent CreateSprite(SpriteRendererComponent renderer, Texture2D texture, int size)
    {
        var spriteData = new SpriteData
        {
            PositionInTexture = new Rectangle(0, 0, size, size),
            Origin = new Point(size / 2, size / 2),
        };
        var sprite = (Sprite)RuntimeHelpers.GetUninitializedObject(typeof(Sprite));
        SetBackingField(sprite, nameof(Sprite.Texture), new CasaEngine.Framework.Assets.Textures.Texture { Resource = texture });
        SetBackingField(sprite, nameof(Sprite.SpriteData), spriteData);

        var component = new StaticSpriteComponent();
        SetField(component, "_spriteData", spriteData);
        SetField(component, "_sprite", sprite);
        SetField(component, "_spriteRendererComponent", renderer);
        return component;
    }

    private static void SetField(object instance, string name, object value)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static Color PixelAt(byte[] pixels, int x, int y)
    {
        var index = (y * TargetSize + x) * 4;
        return new Color(pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]);
    }

    private static void AssertClose(Color expected, Color actual, string what)
    {
        Assert.True(
            Math.Abs(expected.R - actual.R) <= 2 && Math.Abs(expected.G - actual.G) <= 2 && Math.Abs(expected.B - actual.B) <= 2,
            $"{what}: expected {expected}, got {actual}");
    }
}
