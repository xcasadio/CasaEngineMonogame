using System.Globalization;
using CasaEngine.Editor.Controls;
using CasaEngine.Framework.Materials.Definitions;
using CasaEngine.Framework.Materials.Runtime;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>
/// ADR-0093, <c>material.get</c>: every property value comes back as text in the form <c>material.set_property</c>
/// parses, not only strings and enums (numbers, colours and textures used to come back empty).
/// </summary>
public class MaterialSnapshotValueTests
{
    public static TheoryData<MaterialPropertyType, MaterialValue, string> Values => new()
    {
        { MaterialPropertyType.Float, MaterialValue.FromFloat(0.25f), "0.25" },
        { MaterialPropertyType.Float, MaterialValue.FromFloat(0.8154762f), "0.8154762" },
        { MaterialPropertyType.Integer, MaterialValue.FromInteger(-3), "-3" },
        { MaterialPropertyType.Boolean, MaterialValue.FromBoolean(true), "true" },
        { MaterialPropertyType.Color, MaterialValue.FromColor(new Color(10, 20, 30, 40)), "10,20,30,40" },
        { MaterialPropertyType.Vector2, MaterialValue.FromVector2(new Vector2(1.5f, -2f)), "1.5,-2" },
        { MaterialPropertyType.Vector3, MaterialValue.FromVector3(new Vector3(1f, 2.5f, 3f)), "1,2.5,3" },
        { MaterialPropertyType.Vector4, MaterialValue.FromVector4(new Vector4(0f, 0.5f, 1f, 2f)), "0,0.5,1,2" },
        { MaterialPropertyType.Texture, MaterialValue.FromTextureId(new Guid("0f8fad5b-d9cb-469f-a165-70867728950e")), "0f8fad5b-d9cb-469f-a165-70867728950e" },
        { MaterialPropertyType.Texture, MaterialValue.FromTextureId(Guid.Empty), "" },
        { MaterialPropertyType.Enum, MaterialValue.FromEnum("Opaque"), "Opaque" },
        { MaterialPropertyType.String, MaterialValue.FromString("label"), "label" },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void FormatSnapshotValue_WritesTheValueInTheInvariantSetPropertyForm(MaterialPropertyType type, MaterialValue value, string expected)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        try
        {
            Assert.Equal(expected, MaterialAssetInspectorPanel.FormatSnapshotValue(type, value));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void FormatSnapshotValue_NoValue_IsEmpty()
    {
        Assert.Equal(string.Empty, MaterialAssetInspectorPanel.FormatSnapshotValue(MaterialPropertyType.Float, null));
    }
}
