using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen.Gadget
{
    public partial class ScreenGadgetButton
    {

        public static int Num = 0;





        public ScreenGadgetButton()
            : base("Button" + (Num++))
        {
            Width = 200;
            Height = 80;
            FontColor = Color.Black;
            BackgroundColor = Color.White;
        }



        public override void Save(XmlElement el, SaveOption opt)
        {
            XmlElement node;

            base.Save(el, opt);

            int spriteId = Image == null ? int.MaxValue : Image.Id;
            node = el.OwnerDocument.CreateElementWithText("Image", spriteId.ToString());
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElementWithText("SizeImage", Enum.GetName(typeof(SizeImage), SizeImage));
            el.AppendChild(node);
        }

        public override void Save(BinaryWriter bw, SaveOption opt)
        {
            base.Save(bw, opt);

            int spriteId = Image == null ? int.MaxValue : Image.Id;
            bw.Write(spriteId);
            bw.Write((int)SizeImage);
        }

    }
}
