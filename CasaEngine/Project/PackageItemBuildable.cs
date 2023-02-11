using CasaEngine.Game;

namespace CasaEngine.Project
{
    public class PackageItemBuildable
        : PackageItem
    {

        private Type _type;
        private readonly string _xnbName;



        public PackageItemBuildable(Package package, int id, string name, string xnbName, Type type)
            : base(package, id, name)
        {
            _xnbName = xnbName;
            _type = type;
        }



        public override T LoadItem<T>()
        {
            return Engine.Instance.Game.Content.Load<T>(_xnbName);
        }

    }
}
