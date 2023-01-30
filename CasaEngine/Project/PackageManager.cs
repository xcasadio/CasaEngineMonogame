namespace CasaEngine.Project
{
    public class PackageManager
    {
        readonly List<Package> m_Packages = new List<Package>();



        public Package[] Packages => m_Packages.ToArray();


        public PackageManager(ProjectManager projectManager_)
        {
            projectManager_.ProjectLoaded += new EventHandler(OnProjectLoaded);
            projectManager_.ProjectClosed += new EventHandler(OnProjectClosed);
        }



        public void Refresh()
        {
            m_Packages.Clear();

            /*Package pck;

            pck  = new Package("Sprite2D");
            m_Packages.Add(pck);

            foreach (string n in GameInfo.Instance.Asset2DManager.GetAllSprite2DName())
            {
                pck.AddItem(new PackageItemStreamable(pck, 1, n, typeof(Sprite2D).FullName, GameInfo.Instance.Asset2DManager.GetSprite2DByName(n)));
            }

            pck = new Package("Animation2D");
            m_Packages.Add(pck);

            foreach (string n in GameInfo.Instance.Asset2DManager.GetAllAnimation2DName())
            {
                pck.AddItem(new PackageItemStreamable(pck, 1, n, typeof(Animation2D).FullName, GameInfo.Instance.Asset2DManager.GetAnimation2DByName(n)));
            }*/
        }

        public string FindANewPackageName()
        {
            string ret = "New_Package";
            int num = 0;
            bool isOK = false;

            while (isOK == false)
            {
                isOK = true;

                foreach (Package p in m_Packages)
                {
                    if (p.Name.Equals(ret) == true)
                    {
                        isOK = false;
                        num++;
                        ret = "New_Package_" + num;
                        break;
                    }
                }
            }

            return ret;
        }

        public Package GetOrCreatePackage(string name_)
        {
            Package package = GetPackage(name_);

            if (package == null)
            {
                package = CreatePackage(name_);
            }

            return package;
        }

        public Package CreatePackage(string path_)
        {
            Package package = null;

            //Create File ?
            path_ = path_.Replace('\\', '/');
            string pckName = path_;
            int index = pckName.IndexOf('/');
            if (index != -1)
            {
                pckName = pckName.Substring(0, index);
            }

            package = new Package(pckName);
            //package.SaveXml(fullPath, SaveOption.Editor);
            m_Packages.Add(package);

            return package;
        }

        public Package GetPackage(string name_)
        {
            foreach (Package p in m_Packages)
            {
                if (p.Name.Equals(name_) == true)
                {
                    return p;
                }
            }

            return null;
        }

        private void OnProjectLoaded(object sender, EventArgs e)
        {
            Refresh();
        }

        private void OnProjectClosed(object sender, EventArgs e)
        {

        }


        /*public void AddSprites(string packageName_, string[] files_, bool detectAnimation2D_)
        {
            //crop image and copy image in memory
            foreach (string name in files_)
            {
                LogManager.Instance.WriteLineDebug("Analysing image : " + name);

                ProgressChanged("Analysing... " + Path.GetFileName(name));

                Rectangle rect;
                Point point;
                Bitmap bitmap = new Bitmap(name);

                images.Add(name, bitmap);

                if (NeedToBeAnalysed(name) == false)
                {
                    GetBuildInfo(name, out rect, out point);
                    imageOrigins.Add(name, point);
                    imageCrop.Add(name, rect);
                }
                else
                {
                    if (ShrinkBitmap(bitmap, out rect, out point) == true)
                    {
                        imageOrigins.Add(name, point);
                        imageCrop.Add(name, rect);

                        AddBuildInfo(name, rect, point);
                    }
                    else
                    {
                        LogManager.Instance.WriteLineError("PackImageOptimized can't analyzed the image : '" + name + "'");
                        DisposeImages(images);
                        return (int)FailCode.FailedToLoadImage;
                    }
                }
            }
        }*/


    }
}
