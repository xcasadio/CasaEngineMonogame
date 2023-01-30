using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen
{
    public partial class UIScreen
    {
        public override void Save(XmlElement el_, SaveOption opt_)
        {
            base.Save(el_, opt_);
        }

        public override void Save(BinaryWriter bw_, SaveOption opt_)
        {
            base.Save(bw_, opt_);
        }
    }
}
