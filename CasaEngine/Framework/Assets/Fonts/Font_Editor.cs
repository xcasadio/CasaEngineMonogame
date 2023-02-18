using System.ComponentModel;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;

namespace CasaEngine.Framework.Assets.Fonts
{
    public partial class Font
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public Font(string fileName)
            : this()
        {
            ImportFromFile(fileName);
        }

        private void ImportFromFile(string fileName)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(fileName);

            var fontNode = xmlDoc.SelectSingleNode("font");

            //Info = new FontInfo(xmlDoc.SelectSingleNode("font/info"));
            Common = new FontCommon(xmlDoc.SelectSingleNode("font/common"));

            foreach (XmlNode n in xmlDoc.SelectNodes("font/pages/page"))
            {
                _texturesFileNames.Add(n.Attributes["file"].Value);
            }
            //_TextureFileName = xmlDoc.SelectSingleNode("font/pages/page").Attributes["file"].Value;

            foreach (XmlNode n in xmlDoc.SelectNodes("font/chars/char"))
            {
                var f = new FontChar(n);
                Chars.Add(f);
                _charsDic.Add((char)f.Id, f);
            }

            foreach (XmlNode n in xmlDoc.SelectNodes("font/kernings/kerning"))
            {
                Kernings.Add(new FontKerning(n));
            }
        }

        public void Save(XmlElement el, SaveOption opt)
        {

            XmlNode fontNode = el.OwnerDocument.CreateElement("Font");
            el.AppendChild(fontNode);

            Common.Save(fontNode, opt);

            XmlNode pagesNode = el.OwnerDocument.CreateElement("Pages");
            fontNode.AppendChild(pagesNode);

            foreach (var file in _texturesFileNames)
            {
                XmlNode pageNode = el.OwnerDocument.CreateElement("Page");
                pagesNode.AppendChild(pageNode);
                el.OwnerDocument.AddAttribute((XmlElement)pageNode, "file", file);
            }

            XmlNode charsNode = el.OwnerDocument.CreateElement("Chars");
            fontNode.AppendChild(charsNode);

            foreach (var f in Chars)
            {
                f.Save(charsNode, opt);
            }

            XmlNode kerningsNode = el.OwnerDocument.CreateElement("Kernings");
            fontNode.AppendChild(kerningsNode);

            foreach (var kerning in Kernings)
            {
                kerning.Save(kerningsNode, opt);
            }
        }

        public void Save(BinaryWriter bw, SaveOption option)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public List<string> AssetFileNames => _texturesFileNames;
    }
}