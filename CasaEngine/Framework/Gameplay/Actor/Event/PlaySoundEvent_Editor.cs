using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Framework.Gameplay.Actor.Event
{
    public partial class PlaySoundEvent
    {







        public override void Save(XmlElement el, SaveOption option)
        {
            base.Save(el, option);
            el.OwnerDocument.AddAttribute(el, "asset", _assetName);
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            base.Save(bw, option);
            bw.Write(_assetName);
        }

    }
}
