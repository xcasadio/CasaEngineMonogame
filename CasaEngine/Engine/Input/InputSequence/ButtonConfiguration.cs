using System.Xml;
using CasaEngine.Core.Design;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Input.InputSequence
{
    public class ButtonConfiguration
    {
        readonly Dictionary<int, ButtonMapper> _buttonsConfig = new();

        public PlayerIndex PlayerIndex { get; set; }

        public int ButtonCount => _buttonsConfig.Count;

        public Dictionary<int, ButtonMapper>.Enumerator Buttons => _buttonsConfig.GetEnumerator();

        public ButtonMapper GetButton(int code)
        {
            return _buttonsConfig[code];
        }

        public void AddButton(int code, ButtonMapper but)
        {
            _buttonsConfig.Add(code, but);
        }

        public void ReplaceButton(int code, ButtonMapper newBut)
        {
            _buttonsConfig[code] = newBut;
        }

        public void DeleteButton(int code)
        {
            _buttonsConfig.Remove(code);
        }

        public void Save(XmlElement el, SaveOption option)
        {

        }

        public void Load(XmlElement el, SaveOption option)
        {

        }

        public void Save(BinaryWriter bw, SaveOption option)
        {

        }

        public void Load(BinaryReader br, SaveOption option)
        {

        }

    }
}
