namespace CasaEngine.Editor.Tools
{
    public class CustomEditor : Attribute
    {
        private readonly Type _type;

        public CustomEditor(Type type)
        {
            _type = type;
        }

        public override string ToString()
        {
            return _type.FullName;
        }
    }
}
