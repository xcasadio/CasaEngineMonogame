using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Maths.Shape2D;

namespace CasaEngine.Framework.Assets.Graphics2D
{
    public partial class Sprite2DParams
        : ISaveLoad
    {

        private readonly object _tag;



        public object Tag => _tag;


        public Sprite2DParams(Sprite2DParamsType type, Shape2DObject ob, object tag)
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

    }
}
