using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Maths.Shape2D;

namespace CasaEngine.Framework.Assets.Graphics2D;

public class Sprite2DParams : ISaveLoad
{
    private readonly Shape2dObject _shape2dObject;
    private readonly Sprite2DParamsType _type;

    public Shape2dObject Shape2dObject => _shape2dObject;

    public Sprite2DParamsType Type => _type;

    public Sprite2DParams(Sprite2DParamsType type, Shape2dObject ob)
    {
        _type = type;
        _shape2dObject = ob;
    }

    public void Load(XmlElement el, SaveOption option)
    {
        throw new NotImplementedException();
    }

    public void Load(BinaryReader br, SaveOption opt)
    {
        throw new NotImplementedException();
    }

#if EDITOR
    private readonly object _tag;

    public object Tag => _tag;

    public Sprite2DParams(Sprite2DParamsType type, Shape2dObject ob, object tag)
        : this(type, ob)
    {
        _tag = tag;
    }

    public void Save(XmlElement el, SaveOption option)
    {
        throw new NotImplementedException();
    }

    public void Save(BinaryWriter bw, SaveOption opt)
    {
        throw new NotImplementedException();
    }
#endif
}