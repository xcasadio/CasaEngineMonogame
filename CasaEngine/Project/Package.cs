using CasaEngineCommon.Design;
using System.Xml;
using CasaEngineCommon.Logger;

namespace CasaEngine.Project
{
    public class Package
    {



        public Package Parent
        {
            get;
            internal set;
        }

        public List<Package> Children
        {
            get;
            private set;
        }

        public List<PackageItem> Items
        {
            get;
            private set;
        }

        public string Name
        {
            get;
            set;
        }

        public string PathFromProjectPath
        {
            get;
            private set;
        }

        public string PackageFileName
        {
            get;
            private set;
        }

        public string[] SubDirectories
        {
            get
            {
                List<string> res = new List<string>();

                foreach (PackageItem item in Items)
                {
                    if (res.Contains(item.DirectoryPath) == false
                        && string.IsNullOrWhiteSpace(item.DirectoryPath) == false)
                    {
                        res.Add(item.DirectoryPath);
                    }
                }

                return res.ToArray();
            }
        }



        public Package(string name_, Package parent_ = null)
        {
            Parent = parent_;
            Name = name_;
            Items = new List<PackageItem>();
            Children = new List<Package>();
        }




        public void AddItem(PackageItem item_, string subDirectory_ = "")
        {
            Items.Add(item_);
        }

        public T GetItem<T>(string name_) where T : class
        {
            foreach (PackageItem item in Items)
            {
                if (item.Name.Equals(name_) == true)
                {
                    return item as T;
                }
            }

            return null;
        }




        public void LoadXml(string file_, SaveOption opt_)
        {
            if (File.Exists(file_) == false)
            {
                LogManager.Instance.WriteLineError("Package.Load() : Can't open the package file " + file_);
                return;
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(file_);

            PackageFileName = file_;
        }

        public void SaveXml(string file_, SaveOption opt_)
        {
            XmlDocument xmlDoc = new XmlDocument();


            //add in workspace
            /*if (SourceControl.SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
            {
                string wrkspc = SourceControl.SourceControlManager.Instance.Workspace;
                Directory.CreateDirectory(wrkspc + Path.DirectorySeparatorChar + item_.DirectoryPath);
             
                string fullPath = GameInfo.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.PackageDirPath + Path.DirectorySeparatorChar;
                fullPath += path_.Replace('.', Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.GetFullPath(fullPath));
            }*/

            xmlDoc.Save(file_);

            PackageFileName = file_;
        }



        public void LoadBinary(string file_, SaveOption opt_)
        {
            if (File.Exists(file_) == false)
            {
                LogManager.Instance.WriteLineError("Package.Load() : Can't open the package file " + file_);
                return;
            }

            BinaryReader br = new BinaryReader(File.Open(file_, FileMode.Open));

            br.Close();

            PackageFileName = file_;
        }

        public void SaveBinary(string file_, SaveOption opt_)
        {
            BinaryWriter bw = new BinaryWriter(File.Open(file_, FileMode.Create));

            bw.Close();

            PackageFileName = file_;
        }



    }
}
