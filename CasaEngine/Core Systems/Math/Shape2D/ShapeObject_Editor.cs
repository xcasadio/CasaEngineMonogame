using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using CasaEngineCommon.Extension;
using System.ComponentModel;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.Math.Shape2D
{
    /// <summary>
    /// 
    /// </summary>
    public partial class Shape2DObject
    {

        private static readonly int m_Version = 1;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Used to no loop on the Tag object which have EventHandler also
        /// </summary>
        public bool PropertyChangedActivated = true;
        private object m_Tag;



        /// <summary>
        /// Gets/Sets
        /// </summary>
        [Browsable(false)]
        public object Tag
        {
            get { return m_Tag; }
            set { m_Tag = value; }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        public Shape2DObject(Shape2DType type_)
        {
            m_Type = type_;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="o_"></param>
        /// <returns></returns>
        public virtual bool CompareTo(Shape2DObject o_)
        {
            return m_Flag == o_.m_Flag
                && m_Location == o_.m_Location
                && m_Rotation == o_.m_Rotation
                && m_Type == o_.m_Type;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public virtual void Save(XmlElement el_, SaveOption option_)
        {
            el_.OwnerDocument.AddAttribute(el_, "version", m_Version.ToString());
            el_.OwnerDocument.AddAttribute(el_, "type", Enum.GetName(typeof(Shape2DType), m_Type));

            XmlElement el = el_.OwnerDocument.CreateElement("Location", Location);
            el_.AppendChild(el);

            el_.OwnerDocument.AddAttribute(el_, "rotation", m_Rotation.ToString());
            el_.OwnerDocument.AddAttribute(el_, "flag", m_Flag.ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public virtual void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write(m_Version);
            bw_.Write(Enum.GetName(typeof(Shape2DType), m_Type));
            bw_.Write(m_Rotation);
            bw_.Write(m_Flag);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info"></param>
        public void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null
                && PropertyChangedActivated == true)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is Shape2DObject)
            {
                return CompareTo((Shape2DObject)obj);
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return base.ToString();
        }


    }
}
