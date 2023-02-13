using System.Xml;

namespace CasaEngineCommon.Design
{
    public enum SaveOption
    {
        Editor,
        Game
    }

    public interface ISaveLoad
    {
#if EDITOR
        void Save(BinaryWriter bw_, SaveOption option_);
        void Save(XmlElement el_, SaveOption option_);
#endif
        void Load(BinaryReader br_, SaveOption option_);
        void Load(XmlElement el_, SaveOption option_);
    }
}
