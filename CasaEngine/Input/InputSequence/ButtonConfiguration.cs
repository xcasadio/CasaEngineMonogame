using System.Xml;
using Microsoft.Xna.Framework;
using CasaEngineCommon.Design;

namespace CasaEngine.Input
{
    public class ButtonConfiguration
    {
        readonly Dictionary<int, ButtonMapper> m_ButtonsConfig = new Dictionary<int, ButtonMapper>();



        public PlayerIndex PlayerIndex
        {
            get;
            set;
        }

        public int ButtonCount => m_ButtonsConfig.Count;

        public Dictionary<int, ButtonMapper>.Enumerator Buttons => m_ButtonsConfig.GetEnumerator();


        public ButtonMapper GetButton(int code_)
        {
            return m_ButtonsConfig[code_];
        }

        public void AddButton(int code_, ButtonMapper but_)
        {
            m_ButtonsConfig.Add(code_, but_);
        }

        public void ReplaceButton(int code_, ButtonMapper newBut_)
        {
            m_ButtonsConfig[code_] = newBut_;
        }

        public void DeleteButton(int code_)
        {
            m_ButtonsConfig.Remove(code_);
        }

        public void Save(XmlElement el_, SaveOption option_)
        {

        }

        public void Load(XmlElement el_, SaveOption option_)
        {

        }

        public void Save(BinaryWriter bw_, SaveOption option_)
        {

        }

        public void Load(BinaryReader br_, SaveOption option_)
        {

        }

    }
}
