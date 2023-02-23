using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Input.InputSequence
{
    public class ButtonConfiguration
    {
        private readonly Dictionary<int, ButtonMapper> _buttonsConfig = new();

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
    }
}
