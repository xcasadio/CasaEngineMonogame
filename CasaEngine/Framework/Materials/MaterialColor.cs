using Microsoft.Xna.Framework.Graphics;
using System.Collections;
using System.Globalization;

namespace CasaEngine.Framework.Materials;

public class MaterialColor : MaterialAsset
{
    public Color Color { get; set; }

    public string WriteColor()
    {
        var vector4 = Color.ToVector4();
        return $"float4({vector4.X.ToString(CultureInfo.InvariantCulture)}, {vector4.Y.ToString(CultureInfo.InvariantCulture)}, {vector4.Z.ToString(CultureInfo.InvariantCulture)}, {vector4.W.ToString(CultureInfo.InvariantCulture)})";
    }

    public override void Accept(IMaterialAssetVisitor visitor)
    {
        visitor.Visit(this);
    }

}