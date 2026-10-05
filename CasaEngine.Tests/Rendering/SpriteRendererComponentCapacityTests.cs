using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The sprite queue of <see cref="SpriteRendererComponent"/> is not capped at 10 000 entries any more (ADR-0051): the
/// vertex staging array grows with the queue. The GPU upload cannot run headless, so the test calls the internal
/// <c>FillVertices</c> step that <c>UpdateBuffer</c> runs before the upload. The vertex buffer growth is proved on the
/// device by the "Sprite queue capacity" demo.
/// </summary>
public class SpriteRendererComponentCapacityTests
{
    private const int TextureSize = 128;

    [Fact]
    public void FillVertices_WithMoreThan10000Entries_WritesEveryEntry()
    {
        var component = CreateComponent();
        var texture = CreateTexture(TextureSize, TextureSize);

        // Entries 0 to 10 000 in increasing key order: the sort keeps the submission order. The first one samples the
        // texel (1, 0), the last (the 10 001st) the texel (5, 0).
        for (var i = 0; i <= 10000; i++)
        {
            var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, i, 0, 0, 0, 0);
            var source = new Rectangle(i == 10000 ? 5 : 1, 0, 1, 1);
            component.DrawSprite(texture, source, Point.Zero, Vector2.Zero, 0f, Vector2.One, Color.White, 0f,
                in key, SpriteEffects.None, Rectangle.Empty);
        }

        var written = component.FillVertices();

        Assert.Equal(40004, written);
        var vertices = GetVertices(component);
        Assert.True(vertices.Length >= 40004);
        Assert.Equal(new Vector2(1f / TextureSize, 0f), vertices[0].TextureCoordinate);
        Assert.Equal(new Vector2(5f / TextureSize, 0f), vertices[40000].TextureCoordinate);
        Assert.Equal(new Vector2(6f / TextureSize, 0f), vertices[40001].TextureCoordinate);
        Assert.Equal(new Vector2(6f / TextureSize, 1f / TextureSize), vertices[40002].TextureCoordinate);
        Assert.Equal(new Vector2(5f / TextureSize, 1f / TextureSize), vertices[40003].TextureCoordinate);
    }

    [Fact]
    public void FillVertices_WithTenThousandEntriesOrFewer_KeepsTheStagingArray()
    {
        var component = CreateComponent();
        var texture = CreateTexture(TextureSize, TextureSize);
        var before = GetVertices(component);

        for (var i = 0; i < 10; i++)
        {
            var key = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, i, 0, 0, 0, 0);
            component.DrawSprite(texture, new Rectangle(1, 0, 1, 1), Point.Zero, Vector2.Zero, 0f, Vector2.One, Color.White, 0f,
                in key, SpriteEffects.None, Rectangle.Empty);
        }

        Assert.Equal(40, component.FillVertices());
        Assert.Same(before, GetVertices(component));
    }

    private static SpriteRendererComponent CreateComponent()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var components = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(components);
        components!.SetValue(game, new GameComponentCollection());
        return new SpriteRendererComponent(game);
    }

    private static Texture2D CreateTexture(int width, int height)
    {
        var texture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
        var widthField = FindIntField(typeof(Texture2D), "width");
        var heightField = FindIntField(typeof(Texture2D), "height");
        widthField.SetValue(texture, width);
        heightField.SetValue(texture, height);
        return texture;
    }

    private static FieldInfo FindIntField(Type type, string nameFragment)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType == typeof(int) && field.Name.Contains(nameFragment, StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }
            }
        }

        throw new InvalidOperationException($"No int field named like '{nameFragment}' on {type.Name}.");
    }

    private static VertexPositionTexture[] GetVertices(SpriteRendererComponent component)
    {
        var field = typeof(SpriteRendererComponent).GetField("_vertices", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (VertexPositionTexture[])field!.GetValue(component)!;
    }
}
