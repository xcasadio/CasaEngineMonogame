using CasaEngineCommon.Logger;
using System.ComponentModel;

namespace Editor.Sprite2DEditor.SpriteSheetPacker.sspack
{
    /// <summary>
    /// 
    /// </summary>
    public class Builder
    {
        static private readonly string tmpDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Editor.Sprite2DEditor.SpriteSheetPacker");

        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        private int m_PercentTotal, m_Percent;

        private void CreateTemporaryDirectory()
        {
            if (!Directory.Exists(tmpDirectory))
            {
                Directory.CreateDirectory(tmpDirectory);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="build_"></param>
        /// <param name="spriteSheetFileName"></param>
        /// <param name="mapFileName"></param>
        /// <returns></returns>
        public int Build(SpriteSheetTask.SpriteSheetBuild build_, out string spriteSheetFileName, out string mapFileName)
        {
            m_PercentTotal = build_.Files.Count + 6;
            m_Percent = 0;

            SetProgress(1, "Initializing...");

            Exporters.Load();

            CreateTemporaryDirectory();

            mapFileName = tmpDirectory + "\\" + Path.GetFileNameWithoutExtension(build_.SpriteSheetName) + ".txt";
            spriteSheetFileName = tmpDirectory + "\\" + build_.SpriteSheetName + ".png";

            // try to find matching exporters
            IImageExporter imageExporter = null;
            IMapExporter mapExporter = null;

            string imageExtension = "png";
            foreach (var exporter in Exporters.ImageExporters)
            {
                if (exporter.ImageExtension.ToLower() == imageExtension)
                {
                    imageExporter = exporter;
                    break;
                }
            }

            if (imageExporter == null)
            {
                LogManager.Instance.WriteLineError("Failed to find exporters for specified image type.");
                return (int)FailCode.ImageExporter;
            }

            foreach (var exporter in Exporters.MapExporters)
            {
                if (exporter.MapExtension.ToLower().Equals("txt") == true)
                {
                    mapExporter = exporter;
                    break;
                }
            }

            if (mapExporter == null)
            {
                LogManager.Instance.WriteLineError("Failed to find exporters for specified map type.");
                return (int)FailCode.MapExporter;
            }

            SetProgress(1, "Checking...");

            foreach (var str in build_.Files)
            {
                if (MiscHelper.IsImageFile(str) == false)
                {
                    return (int)FailCode.FailedToLoadImage;
                }
            }

            // make sure no images have the same name if we're building a map
            if (mapExporter != null)
            {
                for (int i = 0; i < build_.Files.Count; i++)
                {
                    string str1 = Path.GetFileNameWithoutExtension(build_.Files[i]);

                    for (int j = i + 1; j < build_.Files.Count; j++)
                    {
                        string str2 = Path.GetFileNameWithoutExtension(build_.Files[j]);

                        if (str1 == str2)
                        {
                            LogManager.Instance.WriteLineError("Two images have the same name : \n" + build_.Files[i] + "\n" + build_.Files[j]);
                            return (int)FailCode.ImageNameCollision;
                        }
                    }
                }
            }

            SetProgress(0, "Packing image...");

            // generate our output
            ImagePacker imagePacker = new ImagePacker();
            imagePacker.OnProgressChanged += new EventHandler(imagePacker_OnProgressChanged);
            Bitmap outputImage;
            Dictionary<string, SpriteInfo> outputMap;

            // pack the image, generating a map only if desired
            int result = imagePacker.PackImageOptimized(
                build_.Files, build_.PowerOfTwo, build_.Square,
                build_.SpriteSheetWidth, build_.SpriteSheetHeight,
                build_.Padding, mapExporter != null, out outputImage, out outputMap);

            if (result != 0)
            {
                string msg = "There was an error making the image sheet.";

                if (Enum.IsDefined(typeof(FailCode), result) == true)
                {
                    msg = ErrorCodeToString((FailCode)result);
                }

                LogManager.Instance.WriteLineError(msg);
                return result;
            }

            SetProgress(0, "Saving...");

            // try to save using our exporters
            try
            {
                if (File.Exists(spriteSheetFileName))
                {
                    File.Delete(spriteSheetFileName);
                }

                imageExporter.Save(spriteSheetFileName, outputImage);
            }
            catch (Exception e)
            {
                LogManager.Instance.WriteLineError("Error saving file: ");
                LogManager.Instance.WriteException(e);
                return (int)FailCode.FailedToSaveImage;
            }
            finally
            {
                outputImage.Dispose();
            }

            if (mapExporter != null)
            {
                try
                {
                    if (File.Exists(mapFileName))
                    {
                        File.Delete(mapFileName);
                    }

                    mapExporter.Save(mapFileName, outputMap);
                }
                catch (Exception e)
                {
                    LogManager.Instance.WriteLineError("Error saving file: " + e.Message);
                    return (int)FailCode.FailedToSaveMap;
                }
            }

            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void imagePacker_OnProgressChanged(object sender, EventArgs e)
        {
            string msg = "Packing image...";

            if (sender is string)
            {
                msg += "\n" + sender as string;
            }

            SetProgress(1, msg);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="percent_"></param>
        /// <param name="msg_"></param>
        private void SetProgress(int percent_, string msg_ = null)
        {
            m_Percent += percent_;

            if (ProgressChanged != null)
            {
                ProgressChanged.Invoke(this,
                    new ProgressChangedEventArgs((int)((float)m_Percent / (float)m_PercentTotal * 100.0f), msg_));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="code_"></param>
        /// <returns></returns>
        static public string ErrorCodeToString(FailCode code_)
        {
            string msg = "Error to create the spritesheet";

            switch (code_)
            {
                case FailCode.FailedParsingArguments:
                    msg = "Failed Parsing Arguments";
                    break;

                case FailCode.ImageExporter:
                    msg = "Image Exporter error";
                    break;

                case FailCode.MapExporter:
                    msg = "Map Exporter error";
                    break;

                case FailCode.NoImages:
                    msg = "No Images";
                    break;

                case FailCode.ImageNameCollision:
                    msg = "Several images have the same name";
                    break;

                case FailCode.FailedToLoadImage:
                    msg = "Failed To Load Image";
                    break;

                case FailCode.FailedToPackImage:
                    msg = "Failed To Pack Image. Maybe there are too many images. Please insert less images into the sprite sheet.";
                    break;

                case FailCode.FailedToCreateImage:
                    msg = "Failed To Create Image";
                    break;

                case FailCode.FailedToSaveImage:
                    msg = "Failed To Save Image";
                    break;

                case FailCode.FailedToSaveMap:
                    msg = "Failed To Save Map";
                    break;
            }

            return msg;
        }
    }
}
