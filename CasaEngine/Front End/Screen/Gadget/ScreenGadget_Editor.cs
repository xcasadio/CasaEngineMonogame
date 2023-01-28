using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Xml;
using CasaEngine.Game;
using CasaEngineCommon.Extension;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen.Gadget
{
    /// <summary>
    /// 
    /// </summary>
    public partial class ScreenGadget
    {

        static private readonly int m_Version = 1;





        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        protected ScreenGadget(string name_)
        {
            Scale = Vector2.One;
            Name = name_;
            Text = Name;
            Font = Engine.Instance.DefaultSpriteFont;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        public virtual void Save(XmlElement el_, SaveOption opt_)
        {
            XmlElement node;

            el_.OwnerDocument.AddAttribute(el_, "type", GetType().Name);
            el_.OwnerDocument.AddAttribute(el_, "version", m_Version.ToString());

            node = el_.OwnerDocument.CreateElementWithText("AutoSize", AutoSize.ToString());
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElement("BackgroundColor", BackgroundColor);
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElement("FontName", FontName);
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElement("FontColor", FontColor);
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElement("Width", Width.ToString());
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElement("Height", Height.ToString());
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElement("TabIndex", TabIndex.ToString());
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElement("Location", Location);
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElement("Scale", Scale);
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElement("Name", Name);
            el_.AppendChild(node);
            node = el_.OwnerDocument.CreateElement("Text", Text);
            el_.AppendChild(node);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        public virtual void Save(BinaryWriter bw_, SaveOption opt_)
        {
            bw_.Write(GetType().Name);
            bw_.Write(m_Version);
            bw_.Write(AutoSize);
            bw_.Write(BackgroundColor);
            bw_.Write(FontName);
            bw_.Write(FontColor);
            bw_.Write(Width);
            bw_.Write(Height);
            bw_.Write(TabIndex);
            bw_.Write(Location);
            bw_.Write(Scale);
            bw_.Write(Name);
            bw_.Write(Text);
        }

    }
}
