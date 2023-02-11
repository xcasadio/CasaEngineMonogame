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





        public Font(string fileName)
            : this()
        {
            ImportFromFile(fileName);
        }



        public override bool CompareTo(BaseObject other)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private void ImportFromFile(string fileName)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(fileName);

            XmlNode fontNode = xmlDoc.SelectSingleNode("font");

            //Info = new FontInfo(xmlDoc.SelectSingleNode("font/info"));
            Common = new FontCommon(xmlDoc.SelectSingleNode("font/common"));

            foreach (XmlNode n in xmlDoc.SelectNodes("font/pages/page"))
            {
                _texturesFileNames.Add(n.Attributes["file"].Value);
            }
            //_TextureFileName = xmlDoc.SelectSingleNode("font/pages/page").Attributes["file"].Value;

            foreach (XmlNode n in xmlDoc.SelectNodes("font/chars/char"))
            {
                FontChar f = new FontChar(n);
                Chars.Add(f);
                _charsDic.Add((char)f.Id, f);
            }

            foreach (XmlNode n in xmlDoc.SelectNodes("font/kernings/kerning"))
            {
                Kernings.Add(new FontKerning(n));
            }
        }

        public override void Save(XmlElement el, SaveOption opt)
        {
            base.Save(el, opt);

            XmlNode fontNode = el.OwnerDocument.CreateElement("Font");
            el.AppendChild(fontNode);

            Common.Save(fontNode, opt);

            XmlNode pagesNode = el.OwnerDocument.CreateElement("Pages");
            fontNode.AppendChild(pagesNode);

            foreach (string file in _texturesFileNames)
            {
                XmlNode pageNode = el.OwnerDocument.CreateElement("Page");
                pagesNode.AppendChild(pageNode);
                el.OwnerDocument.AddAttribute((XmlElement)pageNode, "file", file);
            }

            XmlNode charsNode = el.OwnerDocument.CreateElement("Chars");
            fontNode.AppendChild(charsNode);

            foreach (FontChar f in Chars)
            {
                f.Save(charsNode, opt);
            }

            XmlNode kerningsNode = el.OwnerDocument.CreateElement("Kernings");
            fontNode.AppendChild(kerningsNode);

            foreach (FontKerning kerning in Kernings)
            {
                kerning.Save(kerningsNode, opt);
            }
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            throw new Exception("The method or operation is not implemented.");
        }


        public List<string> AssetFileNames => _texturesFileNames;
    }
}