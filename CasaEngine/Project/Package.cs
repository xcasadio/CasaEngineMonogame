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



        public Package(string name, Package parent = null)
        {
            Parent = parent;
            Name = name;
            Items = new List<PackageItem>();
            Children = new List<Package>();
        }




        public void AddItem(PackageItem ite, string subDirectory = "")
        {
            Items.Add(ite);
        }

        public T GetItem<T>(string name) where T : class
        {
            foreach (PackageItem item in Items)
            {
                if (item.Name.Equals(name) == true)
                {
                    return item as T;
                }
            }

            return null;
        }




        public void LoadXml(string file, SaveOption opt)
        {
            if (File.Exists(file) == false)
            {
                LogManager.Instance.WriteLineError("Package.Load() : Can't open the package file " + file);
                return;
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(file);

            PackageFileName = file;
        }

        public void SaveXml(string file, SaveOption opt)
        {
            XmlDocument xmlDoc = new XmlDocument();


            //add in workspace
            /*if (SourceControl.SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
            {
                string wrkspc = SourceControl.SourceControlManager.Instance.Workspace;
                Directory.CreateDirectory(wrkspc + Path.DirectorySeparatorChar + ite_.DirectoryPath);
             
                string fullPath = GameInfo.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.PackageDirPath + Path.DirectorySeparatorChar;
                fullPath += path_.Replace('.', Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.GetFullPath(fullPath));
            }*/

            xmlDoc.Save(file);

            PackageFileName = file;
        }



        public void LoadBinary(string file, SaveOption opt)
        {
            if (File.Exists(file) == false)
            {
                LogManager.Instance.WriteLineError("Package.Load() : Can't open the package file " + file);
                return;
            }

            BinaryReader br = new BinaryReader(File.Open(file, FileMode.Open));

            br.Close();

            PackageFileName = file;
        }

        public void SaveBinary(string file, SaveOption opt)
        {
            BinaryWriter bw = new BinaryWriter(File.Open(file, FileMode.Create));

            bw.Close();

            PackageFileName = file;
        }



    }
}
