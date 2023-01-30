using CasaEngine.Game;

namespace CasaEngine.Project
{
    public class PackageItemBuildable
        : PackageItem
    {

        private Type m_Type;
        private readonly string m_XnbName;



        public PackageItemBuildable(Package package_, int id_, string name_, string xnbName_, Type type_)
            : base(package_, id_, name_)
        {
            m_XnbName = xnbName_;
            m_Type = type_;
        }



        public override T LoadItem<T>()
        {
            return Engine.Instance.Game.Content.Load<T>(m_XnbName);
        }

    }
}
