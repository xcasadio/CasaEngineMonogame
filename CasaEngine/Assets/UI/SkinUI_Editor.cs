using CasaEngine.Gameplay.Actor.Object;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Assets.UI
{
    public partial class SkinUI
    {





        public SkinUI()
        {

        }



        public override bool CompareTo(BaseObject other_)
        {
            throw new NotImplementedException();
        }

        public override void Save(XmlElement el_, SaveOption option_)
        {
            base.Save(el_, option_);


        }

        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            throw new Exception("The method or operation is not implemented.");
        }

    }
}
