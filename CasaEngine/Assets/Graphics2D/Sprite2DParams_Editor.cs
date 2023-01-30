using CasaEngine.Math.Shape2D;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Assets.Graphics2D
{
    public partial class Sprite2DParams
        : ISaveLoad
    {

        private readonly object m_Tag;



        public object Tag => m_Tag;


        public Sprite2DParams(Sprite2DParamsType type_, Shape2DObject ob_, object tag_)
            : this(type_, ob_)
        {
            m_Tag = tag_;
        }



        public void Save(XmlElement el_, SaveOption option_)
        {
            throw new NotImplementedException();
        }

        public void Save(BinaryWriter bw_, SaveOption opt_)
        {
            throw new NotImplementedException();
        }

    }
}
