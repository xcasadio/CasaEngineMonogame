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

        private readonly Shape2DObject m_Shape2DObject;
        private readonly Sprite2DParamsType m_Type;



        public Shape2DObject Shape2DObject => m_Shape2DObject;

        public Sprite2DParamsType Type => m_Type;


        public Sprite2DParams(Sprite2DParamsType type_, Shape2DObject ob_)
        {
            m_Type = type_;
            m_Shape2DObject = ob_;
        }



        public void Load(XmlElement el_, SaveOption option_)
        {
            throw new NotImplementedException();
        }

        public void Load(BinaryReader br_, SaveOption opt_)
        {
            throw new NotImplementedException();
        }

    }
}
