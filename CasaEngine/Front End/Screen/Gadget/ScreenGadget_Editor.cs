using Microsoft.Xna.Framework;
using System.Xml;
using CasaEngine.Game;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen.Gadget
{
    public partial class ScreenGadget
    {

        static private readonly int Version = 1;





        protected ScreenGadget(string name)
        {
            Scale = Vector2.One;
            Name = name;
            Text = Name;
            Font = Engine.Instance.DefaultSpriteFont;
        }



        public virtual void Save(XmlElement el, SaveOption opt)
        {
            XmlElement node;

            el.OwnerDocument.AddAttribute(el, "type", GetType().Name);
            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());

            node = el.OwnerDocument.CreateElementWithText("AutoSize", AutoSize.ToString());
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElement("BackgroundColor", BackgroundColor);
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElement("FontName", FontName);
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElement("FontColor", FontColor);
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElement("Width", Width.ToString());
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElement("Height", Height.ToString());
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElement("TabIndex", TabIndex.ToString());
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElement("Location", Location);
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElement("Scale", Scale);
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElement("Name", Name);
            el.AppendChild(node);
            node = el.OwnerDocument.CreateElement("Text", Text);
            el.AppendChild(node);
        }

        public virtual void Save(BinaryWriter bw, SaveOption opt)
        {
            bw.Write(GetType().Name);
            bw.Write(Version);
            bw.Write(AutoSize);
            bw.Write(BackgroundColor);
            bw.Write(FontName);
            bw.Write(FontColor);
            bw.Write(Width);
            bw.Write(Height);
            bw.Write(TabIndex);
            bw.Write(Location);
            bw.Write(Scale);
            bw.Write(Name);
            bw.Write(Text);
        }

    }
}
