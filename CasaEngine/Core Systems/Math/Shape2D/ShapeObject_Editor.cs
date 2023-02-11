using System.Xml;
using CasaEngineCommon.Extension;
using System.ComponentModel;
using CasaEngineCommon.Design;

namespace CasaEngine.Math.Shape2D
{
    public partial class Shape2DObject
    {

        private static readonly int Version = 1;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool PropertyChangedActivated = true;
        private object _tag;



        [Browsable(false)]
        public object Tag
        {
            get => _tag;
            set => _tag = value;
        }



        public Shape2DObject(Shape2DType type)
        {
            _type = type;
        }



        public virtual bool CompareTo(Shape2DObject o)
        {
            return _flag == o._flag
                && _location == o._location
                && _rotation == o._rotation
                && _type == o._type;
        }

        public virtual void Save(XmlElement el, SaveOption option)
        {
            el_.OwnerDocument.AddAttribute(el_, "version", Version.ToString());
            el_.OwnerDocument.AddAttribute(el_, "type", Enum.GetName(typeof(Shape2DType), _type));

            XmlElement el = el_.OwnerDocument.CreateElement("Location", Location);
            el_.AppendChild(el);

            el_.OwnerDocument.AddAttribute(el_, "rotation", _rotation.ToString());
            el_.OwnerDocument.AddAttribute(el_, "flag", _flag.ToString());
        }

        public virtual void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write(Version);
            bw.Write(Enum.GetName(typeof(Shape2DType), _type));
            bw.Write(_rotation);
            bw.Write(_flag);
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
