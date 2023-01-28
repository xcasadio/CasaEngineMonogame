using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Game;
using System.IO;
using CasaEngineCommon.Design;
using CasaEngineCommon.Logger;
using Microsoft.Xna.Framework;
using CasaEngine.Graphics2D;

namespace CasaEngine.Project
{
    /// <summary>
    /// 
    /// </summary>
    public class PackageManager
    {

        List<Package> m_Packages = new List<Package>();



        /// <summary>
        /// Gets
        /// </summary>
        public Package[] Packages
        {
            get { return m_Packages.ToArray(); }
        }



        /// <summary>
        /// 
        /// </summary>
        public PackageManager(ProjectManager projectManager_)
        {
            projectManager_.ProjectLoaded += new EventHandler(OnProjectLoaded);
            projectManager_.ProjectClosed += new EventHandler(OnProjectClosed);
        }



        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_">Package name</param>
        public Package GetOrCreatePackage(string name_)
        {
            Package package = GetPackage(name_);

            if (package == null)
            {
                package = CreatePackage(name_);
            }

            return package;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_">path : pckName/Subdirectory/FileName</param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_">Package name</param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnProjectLoaded(object sender, EventArgs e)
        {
            Refresh();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnProjectClosed(object sender, EventArgs e)
        {

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="packageName_"></param>
        /// <param name="files_"></param>
        /// <param name="detectAnimation2D_"></param>
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
