using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen.Gadget
{
    public partial class ScreenGadgetButton
    {

        public static int num = 0;





        public ScreenGadgetButton()
            : base("Button" + (num++))
        {
            Width = 200;
            Height = 80;
            FontColor = Color.Black;
            BackgroundColor = Color.White;
        }



        public override void Save(XmlElement el_, SaveOption opt_)
        {
            XmlElement node;

            base.Save(el_, opt_);

            int spriteID = Image == null ? int.MaxValue : Image.ID;
            node = el_.OwnerDocument.CreateElementWithText("Image", spriteID.ToString());
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElementWithText("SizeImage", Enum.GetName(typeof(SizeImage), SizeImage));
            el_.AppendChild(node);
        }

        public override void Save(BinaryWriter bw_, SaveOption opt_)
        {
            base.Save(bw_, opt_);

            int spriteID = Image == null ? int.MaxValue : Image.ID;
            bw_.Write(spriteID);
            bw_.Write((int)SizeImage);
        }

    }
}
