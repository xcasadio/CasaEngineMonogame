namespace CasaEngine.Framework.GUI.NoesisGUIWrapper.Helpers
{
    internal class XnaKeysComparer : IEqualityComparer<Keys>
    {
        public static readonly XnaKeysComparer Instance = new();

        private XnaKeysComparer()
        {
        }

        public bool Equals(Keys x, Keys y)
        {
            return x == y;
        }

        public int GetHashCode(Keys obj)
        {
            return (int)obj;
        }
    }
}