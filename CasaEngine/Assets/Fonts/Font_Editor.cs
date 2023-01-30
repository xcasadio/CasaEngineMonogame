using System.Xml;
using CasaEngineCommon.Design;
using CasaEngineCommon.Extension;
using CasaEngine.Gameplay.Actor.Object;

#if EDITOR
using System.ComponentModel;
#endif

namespace CasaEngine.Asset.Fonts
{
    public partial class Font
    {
        public event PropertyChangedEventHandler PropertyChanged;





        public Font(string fileName_)
            : this()
        {
            ImportFromFile(fileName_);
        }



        public override bool CompareTo(BaseObject other_)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private void ImportFromFile(string fileName_)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(fileName_);

            XmlNode fontNode = xmlDoc.SelectSingleNode("font");

            //Info = new FontInfo(xmlDoc.SelectSingleNode("font/info"));
            Common = new FontCommon(xmlDoc.SelectSingleNode("font/common"));

            foreach (XmlNode n in xmlDoc.SelectNodes("font/pages/page"))
            {
                m_TexturesFileNames.Add(n.Attributes["file"].Value);
            }
            //m_TextureFileName = xmlDoc.SelectSingleNode("font/pages/page").Attributes["file"].Value;

            foreach (XmlNode n in xmlDoc.SelectNodes("font/chars/char"))
            {
                FontChar f = new FontChar(n);
                Chars.Add(f);
                m_CharsDic.Add((char)f.ID, f);
            }

            foreach (XmlNode n in xmlDoc.SelectNodes("font/kernings/kerning"))
            {
                Kernings.Add(new FontKerning(n));
            }
        }

        public override void Save(XmlElement el_, SaveOption opt_)
        {
            base.Save(el_, opt_);

            XmlNode fontNode = el_.OwnerDocument.CreateElement("Font");
            el_.AppendChild(fontNode);

            Common.Save(fontNode, opt_);

            XmlNode pagesNode = el_.OwnerDocument.CreateElement("Pages");
            fontNode.AppendChild(pagesNode);

            foreach (string file in m_TexturesFileNames)
            {
                XmlNode pageNode = el_.OwnerDocument.CreateElement("Page");
                pagesNode.AppendChild(pageNode);
                el_.OwnerDocument.AddAttribute((XmlElement)pageNode, "file", file);
            }

            XmlNode charsNode = el_.OwnerDocument.CreateElement("Chars");
            fontNode.AppendChild(charsNode);

            foreach (FontChar f in Chars)
            {
                f.Save(charsNode, opt_);
            }

            XmlNode kerningsNode = el_.OwnerDocument.CreateElement("Kernings");
            fontNode.AppendChild(kerningsNode);

            foreach (FontKerning kerning in Kernings)
            {
                kerning.Save(kerningsNode, opt_);
            }
        }

        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            throw new Exception("The method or operation is not implemented.");
        }


        public List<string> AssetFileNames => m_TexturesFileNames;
    }
}