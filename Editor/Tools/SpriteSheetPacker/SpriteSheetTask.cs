using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using CasaEngineCommon.Extension;

namespace Editor.Sprite2DEditor.SpriteSheetPacker
{
    /// <summary>
    /// 
    /// </summary>
    public class SpriteSheetTask
    {
        /// <summary>
        /// 
        /// </summary>
        public struct SpriteSheetBuild
        {
            public string SpriteSheetName;
            public List<string> Files;
            public bool DetectAnimations;
            public int SpriteSheetWidth;
            public int SpriteSheetHeight;
            public bool Square;
            public bool PowerOfTwo;
            public int Padding;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="other_"></param>
            /// <returns>true if identical</returns>
            public bool Compare(SpriteSheetBuild other_)
            {
                if (SpriteSheetName.Equals(other_.SpriteSheetName) == false
                    || DetectAnimations != other_.DetectAnimations
                    || SpriteSheetWidth != other_.SpriteSheetWidth
                    || SpriteSheetHeight != other_.SpriteSheetHeight
                    || Square != other_.Square
                    || PowerOfTwo != other_.PowerOfTwo
                    || Padding != other_.Padding)
                {
                    return false;
                }

                return Files.SequenceEqual<string>(other_.Files);
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public SpriteSheetBuild Copy()
            {
                SpriteSheetBuild b = new SpriteSheetBuild();
                b.SpriteSheetName = SpriteSheetName;                
                b.DetectAnimations = DetectAnimations;
                b.SpriteSheetWidth = SpriteSheetWidth;
                b.SpriteSheetHeight = SpriteSheetHeight;
                b.Square = Square;
                b.PowerOfTwo = PowerOfTwo;
                b.Padding = Padding;

                b.Files = new List<string>();

                foreach (string s in Files)
                {
                    b.Files.Add(s);
                }

                return b;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="el_"></param>
            public void Save(XmlElement el_)
            {
                XmlElement node = el_.OwnerDocument.CreateElementWithText("Name", SpriteSheetName);
                el_.AppendChild(node);

                node = el_.OwnerDocument.CreateElementWithText("DetectAnimation", DetectAnimations.ToString());
                el_.AppendChild(node);

                node = el_.OwnerDocument.CreateElementWithText("Width", SpriteSheetWidth.ToString());
                el_.AppendChild(node);

                node = el_.OwnerDocument.CreateElementWithText("Height", SpriteSheetHeight.ToString());
                el_.AppendChild(node);

                node = el_.OwnerDocument.CreateElementWithText("Square", Square.ToString());
                el_.AppendChild(node);

                node = el_.OwnerDocument.CreateElementWithText("PowerOfTwo", PowerOfTwo.ToString());
                el_.AppendChild(node);

                node = el_.OwnerDocument.CreateElementWithText("Padding", Padding.ToString());
                el_.AppendChild(node);

                XmlElement nodeList = el_.OwnerDocument.CreateElement("FileList");
                el_.AppendChild(nodeList);

                foreach (string s in Files)
                {
                    node = el_.OwnerDocument.CreateElementWithText("File", s);
                    nodeList.AppendChild(node);
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="el_"></param>
            public void Load(XmlElement el_)
            {
                SpriteSheetName = el_.SelectSingleNode("Name").InnerText;
                DetectAnimations = bool.Parse(el_.SelectSingleNode("DetectAnimation").InnerText);
                SpriteSheetWidth = int.Parse(el_.SelectSingleNode("Width").InnerText);
                SpriteSheetHeight = int.Parse(el_.SelectSingleNode("Height").InnerText);
                Square = bool.Parse(el_.SelectSingleNode("Square").InnerText);
                PowerOfTwo = bool.Parse(el_.SelectSingleNode("PowerOfTwo").InnerText);
                Padding = int.Parse(el_.SelectSingleNode("Padding").InnerText);

                Files = new List<string>();
                XmlNode nodeList = el_.SelectSingleNode("FileList");

                foreach (XmlNode node in nodeList.ChildNodes)
                {
                    Files.Add(node.InnerText);
                }
            }
        }

        #region Fields

        private List<SpriteSheetBuild> m_SpriteSheet = new List<SpriteSheetBuild>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public int Count
        {
            get { return m_SpriteSheet.Count; }
        }

        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<SpriteSheetBuild> Builds
        {
            get { return m_SpriteSheet; }
        }

        #endregion

        #region Constructors

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="build_"></param>
        public void AddBuild(SpriteSheetBuild build_)
        {
            m_SpriteSheet.Add(build_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        /// <param name="build_"></param>
        public void SetBuild(int index_, SpriteSheetBuild build_)
        {
            m_SpriteSheet[index_] = build_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="build_"></param>
        public void RemoveBuild(SpriteSheetBuild build_)
        {
            m_SpriteSheet.Remove(build_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        public void RemoveAt(int index_)
        {
            m_SpriteSheet.RemoveAt(index_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        /// <returns></returns>
        public SpriteSheetBuild GetBuild(int index_)
        {
            return m_SpriteSheet[index_];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public SpriteSheetTask Copy()
        {
            SpriteSheetTask res = new SpriteSheetTask();

            res.Name = Name;

            foreach (SpriteSheetBuild b in m_SpriteSheet)
            {
                res.m_SpriteSheet.Add(b.Copy());
            }

            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="saveOption_"></param>
        public void Load(XmlElement el_)
        {
            Name = el_.Attributes["name"].Value;

            XmlElement nodeList = (XmlElement)el_.SelectSingleNode("BuildList");

            foreach (XmlNode node in nodeList.ChildNodes)
            {
                SpriteSheetBuild b = new SpriteSheetBuild();
                b.Load((XmlElement)node);
                AddBuild(b);
            } 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="saveOption_"></param>
        public void Save(XmlElement el_)
        {
            el_.OwnerDocument.AddAttribute(el_, "name", Name);

            XmlElement nodeList = el_.OwnerDocument.CreateElement("BuildList");
            el_.AppendChild(nodeList);

            foreach (SpriteSheetBuild b in m_SpriteSheet)
            {
                XmlElement buildNode = el_.OwnerDocument.CreateElement("Build");
                nodeList.AppendChild(buildNode);
                b.Save(buildNode);
            }            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        /// <returns>true if identical</returns>
        public bool Compare(SpriteSheetTask other_)
        {
            if (Name.Equals(other_.Name) == false)
            {
                return false;
            }

            if (m_SpriteSheet.Count != other_.m_SpriteSheet.Count)
            {
                return false;
            }

            for (int i = 0; i < m_SpriteSheet.Count; i++)
            {
                if (m_SpriteSheet[i].Compare(other_.m_SpriteSheet[i]) == false)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
