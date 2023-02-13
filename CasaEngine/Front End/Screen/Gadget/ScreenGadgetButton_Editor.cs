using System.Xml;
using CasaEngineCommon.Design;
using CasaEngineCommon.Extension;

namespace CasaEngine.Front_End.Screen.Gadget
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

            var spriteId = Image == null ? int.MaxValue : Image.Id;
            node = el.OwnerDocument.CreateElementWithText("Image", spriteId.ToString());
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElementWithText("SizeImage", Enum.GetName(typeof(SizeImage), SizeImage));
            el.AppendChild(node);
        }

        public override void Save(BinaryWriter bw, SaveOption opt)
        {
            base.Save(bw, opt);

            var spriteId = Image == null ? int.MaxValue : Image.Id;
            bw.Write(spriteId);
            bw.Write((int)SizeImage);
        }

    }
}
