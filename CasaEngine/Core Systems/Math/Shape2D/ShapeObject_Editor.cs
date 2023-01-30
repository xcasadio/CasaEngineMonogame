using System.Xml;
using CasaEngineCommon.Extension;
using System.ComponentModel;
using CasaEngineCommon.Design;

namespace CasaEngine.Math.Shape2D
{
    public partial class Shape2DObject
    {

        private static readonly int m_Version = 1;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool PropertyChangedActivated = true;
        private object m_Tag;



        [Browsable(false)]
        public object Tag
        {
            get => m_Tag;
            set => m_Tag = value;
        }



        public Shape2DObject(Shape2DType type_)
        {
            m_Type = type_;
        }



        public virtual bool CompareTo(Shape2DObject o_)
        {
            return m_Flag == o_.m_Flag
                && m_Location == o_.m_Location
                && m_Rotation == o_.m_Rotation
                && m_Type == o_.m_Type;
        }

        public virtual void Save(XmlElement el_, SaveOption option_)
        {
            el_.OwnerDocument.AddAttribute(el_, "version", m_Version.ToString());
            el_.OwnerDocument.AddAttribute(el_, "type", Enum.GetName(typeof(Shape2DType), m_Type));

            XmlElement el = el_.OwnerDocument.CreateElement("Location", Location);
            el_.AppendChild(el);

            el_.OwnerDocument.AddAttribute(el_, "rotation", m_Rotation.ToString());
            el_.OwnerDocument.AddAttribute(el_, "flag", m_Flag.ToString());
        }

        public virtual void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write(m_Version);
            bw_.Write(Enum.GetName(typeof(Shape2DType), m_Type));
            bw_.Write(m_Rotation);
            bw_.Write(m_Flag);
        }

        public void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null
                && PropertyChangedActivated == true)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is Shape2DObject)
            {
                return CompareTo((Shape2DObject)obj);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return base.ToString();
        }


    }
}
