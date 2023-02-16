using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen
{
    public partial class UiScreen
    {
        public override void Save(XmlElement el, SaveOption opt)
        {
            base.Save(el, opt);
        }

        public override void Save(BinaryWriter bw, SaveOption opt)
        {
            base.Save(bw, opt);
        }
    }
}
