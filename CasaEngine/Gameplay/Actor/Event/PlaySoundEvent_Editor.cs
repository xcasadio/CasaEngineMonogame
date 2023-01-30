using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor.Event
{
    public partial class PlaySoundEvent
    {







        public override void Save(XmlElement el_, SaveOption option_)
        {
            base.Save(el_, option_);
            el_.OwnerDocument.AddAttribute(el_, "asset", m_AssetName);
        }

        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            base.Save(bw_, option_);
            bw_.Write(m_AssetName);
        }

    }
}
