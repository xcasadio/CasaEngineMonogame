using System.Reflection;
using CasaEngineCommon.Design;
using CasaEngineCommon.Logger;
using System.Xml;

namespace CasaEngine.Project
{
    public class PackageItemStreamable
        : PackageItem
    {

        private IPackageable _item;

        private long _filePosition; //use for binary loading
        private readonly string _xmlPath; //use for xml loading
        private Type _itemType;



        public IPackageable Item => _item;

        public string ClassName
        {
            get;
            private set;
        }

        public Type ItemType
        {
            get
            {
                if (_itemType == null)
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    foreach (var a in assemblies)
                    {
                        _itemType = a.GetType(ClassName, false, true);

                        if (_itemType != null)
                        {
                            break;
                        }
                    }

                    if (_itemType == null)
                    {
                        throw new Exception("Can't retrieve the type " + ClassName);
                    }
                }

                return _itemType;
            }
        }



        public PackageItemStreamable(Package package, int id, string name, string className, IPackageable ite, long filePosition = -1)
            : base(package, id, name)
        {
            ClassName = className;
            _xmlPath = "Project/Packages/Package[name='" + Package.Name + "']/PackageItem[id='" + Id + "']";
            _filePosition = filePosition;
            _item = ite;
        }



        public override T LoadItem<T>()
        {
            //throw new NotImplementedException();

            if (_item == null)
            {
                try
                {
                    //if (XML)
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(Package.PackageFileName);
                    var el = (XmlElement)xmlDoc.SelectSingleNode(_xmlPath);
                    //else (Binary)

                    _item = (IPackageable)Activator.CreateInstance(ItemType,
                    new object[] { el, SaveOption.Editor });
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
            }

            if ((_item is T) == false)
            {
                throw new InvalidOperationException("PackageItemStreamable : item is not a '" + typeof(T).Name + "' object.");
            }

            return (T)_item;
        }

    }
}
