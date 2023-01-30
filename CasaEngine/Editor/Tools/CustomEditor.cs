namespace CasaEngine.Editor.Tools
{
    public class CustomEditor
        : Attribute
    {
        private readonly Type m_Type;

        public CustomEditor(Type type_)
        {
            m_Type = type_;
        }

        public override string ToString()
        {
            return m_Type.FullName;
        }
    }
}
