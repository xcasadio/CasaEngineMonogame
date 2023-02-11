using CasaEngine.Gameplay.Actor.Object;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Assets.UI
{
    public partial class SkinUi
    {





        public SkinUi()
        {

        }



        public override bool CompareTo(BaseObject other)
        {
            throw new NotImplementedException();
        }

        public override void Save(XmlElement el, SaveOption option)
        {
            base.Save(el, option);


        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            throw new Exception("The method or operation is not implemented.");
        }

    }
}
