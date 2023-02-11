using CasaEngine.Math.Shape2D;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Assets.Graphics2D
{
    public
#if EDITOR
    partial
#endif
    class Sprite2DParams
        : ISaveLoad
    {

        private readonly Shape2DObject _shape2DObject;
        private readonly Sprite2DParamsType _type;



        public Shape2DObject Shape2DObject => _shape2DObject;

        public Sprite2DParamsType Type => _type;


        public Sprite2DParams(Sprite2DParamsType type, Shape2DObject ob)
        {
            _type = type;
            _shape2DObject = ob;
        }



        public void Load(XmlElement el, SaveOption option)
        {
            throw new NotImplementedException();
        }

        public void Load(BinaryReader br, SaveOption opt)
        {
            throw new NotImplementedException();
        }

    }
}
