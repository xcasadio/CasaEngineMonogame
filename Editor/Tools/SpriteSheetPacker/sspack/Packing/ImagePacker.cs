#region MIT License

/*
 * Copyright (c) 2009-2010 Nick Gravelyn (nick@gravelyn.com), Markus Ewald (cygon@CasaEngine.org)
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a 
 * copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, including without limitation 
 * the rights to use, copy, modify, merge, publish, distribute, sublicense, 
 * and/or sell copies of the Software, and to permit persons to whom the Software 
 * is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
 * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
 * 
 */

#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.ComponentModel;
using CasaEngineCommon.Logger;
using System.Xml;
using CasaEngine.Game;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Packing;
using CasaEngine.Project;

namespace Editor.Sprite2DEditor.SpriteSheetPacker.sspack
{
	public class ImagePacker
	{
        private readonly string dirTemp = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify) + "\\Temp";

        public event EventHandler OnProgressChanged;

        // various properties of the resulting image
		private bool requirePow2, requireSquare;
		private int padding;
		private int outputWidth, outputHeight;

		// the input list of image files
		private List<string> files;

		// some dictionaries to hold the image sizes and destination rectangles
        private readonly Dictionary<string, Point> imageOrigins = new Dictionary<string, Point>();
		private readonly Dictionary<string, Size> imageSizes = new Dictionary<string, Size>();
        private readonly Dictionary<string, Rectangle> imageCrop = new Dictionary<string, Rectangle>();
        private readonly Dictionary<string, Bitmap> images = new Dictionary<string, Bitmap>();
		private readonly Dictionary<string, Rectangle> imagePlacement = new Dictionary<string, Rectangle>();

        private List<SpriteSheetBuildInfo> m_SpriteSheetBuildInfo = new List<SpriteSheetBuildInfo>();


        /// <summary>
		/// Packs a collection of images into a single image.
		/// </summary>
		/// <param name="imageFiles">The list of file paths of the images to be combined.</param>
		/// <param name="requirePowerOfTwo">Whether or not the output image must have a power of two size.</param>
		/// <param name="requireSquareImage">Whether or not the output image must be a square.</param>
		/// <param name="maximumWidth">The maximum width of the output image.</param>
		/// <param name="maximumHeight">The maximum height of the output image.</param>
		/// <param name="imagePadding">The amount of blank space to insert in between individual images.</param>
		/// <param name="generateMap">Whether or not to generate the map dictionary.</param>
		/// <param name="outputImage">The resulting output image.</param>
		/// <param name="outputMap">The resulting output map of placement rectangles for the images.</param>
		/// <returns>0 if the packing was successful, error code otherwise.</returns>
        public int PackImageOptimized(
            IEnumerable<string> imageFiles,
            bool requirePowerOfTwo,
            bool requireSquareImage,
            int maximumWidth,
            int maximumHeight,
            int imagePadding,
            bool generateMap,
            out Bitmap outputImage,
            out Dictionary<string, SpriteInfo> outputMap)
        {
            try
            {
                LogManager.Instance.WriteLineDebug("Initializing...");

                ProgressChanged("Initializing...");

                files = new List<string>(imageFiles);
                requirePow2 = requirePowerOfTwo;
                requireSquare = requireSquareImage;
                outputWidth = maximumWidth;
                outputHeight = maximumHeight;
                padding = imagePadding;

                outputImage = null;
                outputMap = null;

                // make sure our dictionaries are cleared before starting
                imageSizes.Clear();
                imagePlacement.Clear();
                images.Clear();
                imageCrop.Clear();

                if (Directory.Exists(dirTemp) == false)
                {
                    Directory.CreateDirectory(dirTemp);
                }

                ProgressChanged("Load build info file");

                LoadSpriteSheetBuildInfo();

                //crop image and copy image in memory
                foreach (var name in files)
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

                SaveSpriteSheetBuildInfo();

                ProgressChanged("Packing images...");

                ArevaloRectanglePacker rectpacker = new ArevaloRectanglePacker(outputWidth, outputHeight);

                foreach (var key in imageCrop)
                {
                    Microsoft.Xna.Framework.Point point;

                    if (rectpacker.TryPack(key.Value.Width + padding, key.Value.Height + padding, out point) == true)
                    {
                        Rectangle rect = new Rectangle(point.X, point.Y, key.Value.Width + padding, key.Value.Height + padding);
                        imagePlacement.Add(key.Key, rect);
                    }
                    else
                    {
                        DisposeImages(images);
                        return (int)FailCode.FailedToPackImage;
                    }
                }

                ProgressChanged("Saving spritesheet...");

                // make our output image
                outputImage = CreateOutputImageOptimized();
                if (outputImage == null)
                    return (int)FailCode.FailedToSaveImage;

                ProgressChanged("Generating map file...");

                if (generateMap)
                {
                    // go through our image placements and replace the width/height found in there with
                    // each image's actual width/height (since the ones in imagePlacement will have padding)
                    /*string[] keys = new string[imagePlacement.Keys.Count];
                    imagePlacement.Keys.CopyTo(keys, 0);
                    foreach (var k in keys)
                    {
                        // get the actual size
                        Rectangle crop = imageCrop[k];

                        // get the placement rectangle
                        Rectangle r = imagePlacement[k];

                        // set the proper size
                        r.Width = s.Width;
                        r.Height = s.Height;

                        // insert back into the dictionary
                        imagePlacement[k] = r;
                    }*/

                    // copy the placement dictionary to the output
                    outputMap = new Dictionary<string, SpriteInfo>();
                    foreach (var pair in imagePlacement)
                    {
                        SpriteInfo info = new SpriteInfo();
                        info.rectangle = pair.Value;
                        info.origin = imageOrigins[pair.Key];
                        outputMap.Add(pair.Key, info);
                    }
                }

                Directory.Delete(dirTemp, true);

                return 0;
            }
            catch (System.Exception e)
            {
                LogManager.Instance.WriteException(e);
                throw e;
            }
            finally
            {
                DisposeImages(images);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="images"></param>
        private void DisposeImages(Dictionary<string, Bitmap> images_)
        {
            foreach (KeyValuePair<string, Bitmap> i in images_)
            {
                i.Value.Dispose();
            }

            images_.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        private void LoadSpriteSheetBuildInfo()
        {
            string buildFile = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.ConfigDirPath + Path.DirectorySeparatorChar + "SpriteSheetBuildInfo.xml";

            if (File.Exists(buildFile) == false)
            {
                return;
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(buildFile);

            XmlElement elRoot = (XmlElement)xmlDoc.SelectSingleNode("SpriteSheetBuildInfo");

            foreach (XmlNode node in elRoot.SelectNodes("BuildInfo"))
            {
                SpriteSheetBuildInfo info = new SpriteSheetBuildInfo();

                info.FileName = node.Attributes["fileName"].Value;
                info.FileDate = DateTime.Parse(node.Attributes["dateTime"].Value);
                info.FileSize = long.Parse(node.Attributes["fileSize"].Value);
                info.ImageCrop = new Rectangle(
                    int.Parse(node.Attributes["imageCropX"].Value),
                    int.Parse(node.Attributes["imageCropY"].Value),
                    int.Parse(node.Attributes["imageCropW"].Value),
                    int.Parse(node.Attributes["imageCropH"].Value));
                info.ImageOrigin = new Point(
                    int.Parse(node.Attributes["imageOriginX"].Value),
                    int.Parse(node.Attributes["imageOriginY"].Value));

                m_SpriteSheetBuildInfo.Add(info);
            }   
        }

        /// <summary>
        /// 
        /// </summary>
        private void SaveSpriteSheetBuildInfo()
        {
            string buildFile = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.ConfigDirPath + Path.DirectorySeparatorChar + "SpriteSheetBuildInfo.xml";
            
            XmlDocument xmlDoc = new XmlDocument();
            XmlElement elRoot = xmlDoc.AddRootNode("SpriteSheetBuildInfo");

            foreach (SpriteSheetBuildInfo info in m_SpriteSheetBuildInfo)
            {
                XmlElement node = xmlDoc.CreateElement("BuildInfo");
                elRoot.AppendChild(node);

                xmlDoc.AddAttribute((XmlElement)node, "fileName", info.FileName);
                xmlDoc.AddAttribute((XmlElement)node, "dateTime", info.FileDate.ToString());
                xmlDoc.AddAttribute((XmlElement)node, "fileSize", info.FileSize.ToString());
                xmlDoc.AddAttribute((XmlElement)node, "imageCropX", info.ImageCrop.X.ToString());
                xmlDoc.AddAttribute((XmlElement)node, "imageCropY", info.ImageCrop.Y.ToString());
                xmlDoc.AddAttribute((XmlElement)node, "imageCropW", info.ImageCrop.Width.ToString());
                xmlDoc.AddAttribute((XmlElement)node, "imageCropH", info.ImageCrop.Height.ToString());
                xmlDoc.AddAttribute((XmlElement)node, "imageOriginX", info.ImageOrigin.X.ToString());
                xmlDoc.AddAttribute((XmlElement)node, "imageOriginY", info.ImageOrigin.Y.ToString());
            }

            xmlDoc.Save(buildFile);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <returns></returns>
        private bool NeedToBeAnalysed(string fileName_)
        {
            foreach (SpriteSheetBuildInfo info in m_SpriteSheetBuildInfo)
            {
                if (info.FileName.Equals(Path.GetFileName(fileName_)) == true)
                {
                    FileInfo fi = new FileInfo(fileName_);

                    if (fi.Exists == true)
                    {
                        return fi.LastWriteTime.Equals(info.FileDate)
                            && fi.Length == info.FileSize;
                    }

                    break;
                }
            }

            return true;
        }        
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <param name="rect_"></param>
        /// <param name="point_"></param>
        private void GetBuildInfo(string fileName_, out Rectangle rect_, out Point point_)
        {
            rect_ = Rectangle.Empty;
            point_ = Point.Empty;

            foreach (SpriteSheetBuildInfo info in m_SpriteSheetBuildInfo)
            {
                if (info.FileName.Equals(Path.GetFileName(fileName_)) == true)
                {
                    rect_ = info.ImageCrop;
                    point_ = info.ImageOrigin;
                    return;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <param name="rect_"></param>
        /// <param name="point_"></param>
        private void AddBuildInfo(string fileName_, Rectangle rect_, Point point_)
        {
            foreach (SpriteSheetBuildInfo i in m_SpriteSheetBuildInfo)
            {
                if (i.FileName.Equals(Path.GetFileName(fileName_)) == true)
                {
                    throw new InvalidOperationException("ImagePacker.AddBuildInfo() : the SpriteSheetBuildInfo for the image '" + fileName_ + "' already exists !");
                }
            }

            SpriteSheetBuildInfo info = new SpriteSheetBuildInfo();
            FileInfo fi = new FileInfo(fileName_);

            if (fi.Exists == false)
            {
                throw new FileNotFoundException("ImagePacker.AddBuildInfo() : the image '" + fileName_ + "' doesn't exists !");
            }

            info.FileName = Path.GetFileName(fileName_);
            info.FileDate = fi.LastWriteTime;
            info.FileSize = fi.Length;
            info.ImageCrop = rect_;
            info.ImageOrigin = point_;

            m_SpriteSheetBuildInfo.Add(info);
        }

		/// <summary>
		/// Packs a collection of images into a single image.
		/// </summary>
		/// <param name="imageFiles">The list of file paths of the images to be combined.</param>
		/// <param name="requirePowerOfTwo">Whether or not the output image must have a power of two size.</param>
		/// <param name="requireSquareImage">Whether or not the output image must be a square.</param>
		/// <param name="maximumWidth">The maximum width of the output image.</param>
		/// <param name="maximumHeight">The maximum height of the output image.</param>
		/// <param name="imagePadding">The amount of blank space to insert in between individual images.</param>
		/// <param name="generateMap">Whether or not to generate the map dictionary.</param>
		/// <param name="outputImage">The resulting output image.</param>
		/// <param name="outputMap">The resulting output map of placement rectangles for the images.</param>
		/// <returns>0 if the packing was successful, error code otherwise.</returns>
		public int PackImage(
			IEnumerable<string> imageFiles, 
			bool requirePowerOfTwo, 
			bool requireSquareImage, 
			int maximumWidth,
			int maximumHeight,
			int imagePadding,
			bool generateMap,
			out Bitmap outputImage,
            out Dictionary<string, SpriteInfo> outputMap)
		{
            ProgressChanged("Initializing...");

			files = new List<string>(imageFiles);
			requirePow2 = requirePowerOfTwo;
			requireSquare = requireSquareImage;
			outputWidth = maximumWidth;
			outputHeight = maximumHeight;
			padding = imagePadding;

			outputImage = null;
			outputMap = null;

			// make sure our dictionaries are cleared before starting
			imageSizes.Clear();
			imagePlacement.Clear();

            if (Directory.Exists(dirTemp) == true)
            {
                foreach (string f in Directory.GetFiles(dirTemp))
                {
                    FileInfo fi = new FileInfo(f);
                    fi.Attributes = FileAttributes.Normal;
                    File.Delete(f);
                }
            }
            else
            {
                Directory.CreateDirectory(dirTemp);
            }

            List<string> filesTemp = new List<string>();

            // copy image and crop them
            foreach (var image in files)
            {
                ProgressChanged("Analysing... " + Path.GetFileName(image));

                string newFile = dirTemp + "\\" + Path.GetFileName(image);
                File.Copy(image, newFile, true);
                FileInfo fi = new FileInfo(newFile);
                fi.Attributes = FileAttributes.Normal;
                filesTemp.Add(newFile);
                imageSizes.Add(newFile, ShrinkBitmap(newFile));
            }

            files.Clear();
            files.AddRange(filesTemp);
            filesTemp.Clear();

			// get the sizes of all the images
			/*foreach (var image in files)
			{
				Bitmap bitmap = Bitmap.FromFile(image) as Bitmap;
				if (bitmap == null)
					return (int)FailCode.FailedToLoadImage;
				imageSizes.Add(image, bitmap.Size);
			}*/

            ProgressChanged("Prepare pack image...");

			// sort our files by file size so we place large sprites first
			files.Sort(
				(f1, f2) =>
				{
					Size b1 = imageSizes[f1];
					Size b2 = imageSizes[f2];

					int c = -b1.Width.CompareTo(b2.Width);
					if (c != 0)
						return c;

					c = -b1.Height.CompareTo(b2.Height);
					if (c != 0)
						return c;

					return f1.CompareTo(f2);
				});

            ProgressChanged("Saving spritesheet...");

			// try to pack the images
			if (!PackImageRectangles())
				return (int)FailCode.FailedToPackImage;

			// make our output image
			outputImage = CreateOutputImage();
			if (outputImage == null)
				return (int)FailCode.FailedToSaveImage;

            ProgressChanged("Generating map file...");

			if (generateMap)
			{
				// go through our image placements and replace the width/height found in there with
				// each image's actual width/height (since the ones in imagePlacement will have padding)
				string[] keys = new string[imagePlacement.Keys.Count];
				imagePlacement.Keys.CopyTo(keys, 0);
				foreach (var k in keys)
				{
					// get the actual size
					Size s = imageSizes[k];

					// get the placement rectangle
					Rectangle r = imagePlacement[k];

					// set the proper size
					r.Width = s.Width;
					r.Height = s.Height;

					// insert back into the dictionary
					imagePlacement[k] = r;
				}

				// copy the placement dictionary to the output
                outputMap = new Dictionary<string, SpriteInfo>();
				foreach (var pair in imagePlacement)
				{
                    SpriteInfo info = new SpriteInfo();
                    info.rectangle = pair.Value;
                    info.origin = imageOrigins[pair.Key];
					outputMap.Add(pair.Key, info);
				}
			}

			// clear our dictionaries just to free up some memory
			imageSizes.Clear();
			imagePlacement.Clear();

            Directory.Delete(dirTemp, true);

			return 0;
		}

		// This method does some trickery type stuff where we perform the TestPackingImages method over and over, 
		// trying to reduce the image size until we have found the smallest possible image we can fit.
		private bool PackImageRectangles()
		{
			// create a dictionary for our test image placements
			Dictionary<string, Rectangle> testImagePlacement = new Dictionary<string, Rectangle>();

			// get the size of our smallest image
			int smallestWidth = int.MaxValue;
			int smallestHeight = int.MaxValue;
			foreach (var size in imageSizes)
			{
				smallestWidth = Math.Min(smallestWidth, size.Value.Width);
				smallestHeight = Math.Min(smallestHeight, size.Value.Height);
			}

			// we need a couple values for testing
			int testWidth = outputWidth;
			int testHeight = outputHeight;

			bool shrinkVertical = false;

			// just keep looping...
			while (true)
			{
				// make sure our test dictionary is empty
				testImagePlacement.Clear();

				// try to pack the images into our current test size
				if (!TestPackingImages(testWidth, testHeight, testImagePlacement))
				{
					// if that failed...

					// if we have no images in imagePlacement, i.e. we've never succeeded at PackImages,
					// show an error and return false since there is no way to fit the images into our
					// maximum size texture
					if (imagePlacement.Count == 0)
						return false;

					// otherwise return true to use our last good results
					if (shrinkVertical)
						return true;

					shrinkVertical = true;
					testWidth += smallestWidth + padding + padding;
					testHeight += smallestHeight + padding + padding;
					continue;
				}

				// clear the imagePlacement dictionary and add our test results in
				imagePlacement.Clear();
				foreach (var pair in testImagePlacement)
					imagePlacement.Add(pair.Key, pair.Value);

				// figure out the smallest bitmap that will hold all the images
				testWidth = testHeight = 0;
				foreach (var pair in imagePlacement)
				{
					testWidth = Math.Max(testWidth, pair.Value.Right);
					testHeight = Math.Max(testHeight, pair.Value.Bottom);
				}

				// subtract the extra padding on the right and bottom
				if (!shrinkVertical)
					testWidth -= padding;
				testHeight -= padding;

				// if we require a power of two texture, find the next power of two that can fit this image
				if (requirePow2)
				{
					testWidth = MiscHelper.FindNextPowerOfTwo(testWidth);
					testHeight = MiscHelper.FindNextPowerOfTwo(testHeight);
				}

				// if we require a square texture, set the width and height to the larger of the two
				if (requireSquare)
				{
					int max = Math.Max(testWidth, testHeight);
					testWidth = testHeight = max;
				}

				// if the test results are the same as our last output results, we've reached an optimal size,
				// so we can just be done
				if (testWidth == outputWidth && testHeight == outputHeight)
				{
					if (shrinkVertical)
						return true;

					shrinkVertical = true;
				}

				// save the test results as our last known good results
				outputWidth = testWidth;
				outputHeight = testHeight;

				// subtract the smallest image size out for the next test iteration
				if (!shrinkVertical)
					testWidth -= smallestWidth;
				testHeight -= smallestHeight;
			}
		}

		private bool TestPackingImages(int testWidth, int testHeight, Dictionary<string, Rectangle> testImagePlacement)
		{
			// create the rectangle packer
			ArevaloRectanglePacker rectanglePacker = new ArevaloRectanglePacker(testWidth, testHeight);

			foreach (var image in files)
			{
				// get the bitmap for this file
				Size size = imageSizes[image];

				// pack the image
				Microsoft.Xna.Framework.Point origin;
				if (!rectanglePacker.TryPack(size.Width + padding, size.Height + padding, out origin))
				{
					return false;
				}

				// add the destination rectangle to our dictionary
				testImagePlacement.Add(image, new Rectangle(origin.X, origin.Y, size.Width + padding, size.Height + padding));
			}

			return true;
		}


        private Bitmap CreateOutputImageOptimized()
		{
			try
			{
				Bitmap outputImage = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb);

                // draw all the images into the output image
                foreach (var image in files)
                {
                    Bitmap bitmap = images[image];
                    if (bitmap == null)
                        return null;

                    Rectangle location = imagePlacement[image];
                    Rectangle rect = imageCrop[image];                    

                    using (Graphics g = Graphics.FromImage((Image)outputImage))
                        g.DrawImage(bitmap,
                            location,
                            rect.X, rect.Y, rect.Width, rect.Height,
                            GraphicsUnit.Pixel);
                }

				return outputImage;
			}
			catch (Exception e)
			{
                LogManager.Instance.WriteException(e);
				return null;
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
		private Bitmap CreateOutputImage()
		{
			try
			{
				Bitmap outputImage = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb);

                // draw all the images into the output image
                foreach (var image in files)
                {
                    Rectangle location = imagePlacement[image];
                    Bitmap bitmap = Bitmap.FromFile(image) as Bitmap;
                    if (bitmap == null)
                        return null;

                    // copy pixels over to avoid antialiasing or any other side effects of drawing
                    // the subimages to the output image using Graphics
                    /*for (int x = 0; x < bitmap.Width; x++)
                        for (int y = 0; y < bitmap.Height; y++)
                            outputImage.SetPixel(location.X + x, location.Y + y, bitmap.GetPixel(x, y));*/

                    try
                    {
                        using (Graphics g = Graphics.FromImage((Image)outputImage))
                            g.DrawImage(bitmap,
                                new Rectangle(location.X, location.Y,
                                    location.Width, location.Height),
                                0, 0, bitmap.Width, bitmap.Height,
                                GraphicsUnit.Pixel);
                    }
                    finally
                    {
                        bitmap.Dispose();
                    }
                }

				return outputImage;
			}
			catch
			{
				return null;
			}
		}

        /*private Size GetSizeFromBitmap(string file_)
        {
            Bitmap bitmap = Bitmap.FromFile(file_) as Bitmap;
            if (bitmap == null)
                return Size.Empty;

            Size size = bitmap.Size;

            bitmap.Dispose();

            return size;
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file_"></param>
        /// <returns></returns>
        private Size ShrinkBitmap(string file_)
        {
            Bitmap bitmap = Bitmap.FromFile(file_) as Bitmap;
            if (bitmap == null)
                return Size.Empty;

            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            bool ok = false;

            LockBitmap lockBitmap = new LockBitmap(bitmap);
            lockBitmap.LockBits();

            //gauche
            for (int x = 0; x < lockBitmap.Width; x++)
            {
                for (int y = 0; y < lockBitmap.Height; y++)
                {
                    if (lockBitmap.GetPixel(x, y).A > 0)
                    {
                        rect.X = x;
                        ok = true;
                        break;
                    }
                }

                if (ok == true)
                {
                    break;
                }
            }

            //droite
            ok = false;
            for (int x = lockBitmap.Width - 1; x > rect.X; x--)
            {
                for (int y = 0; y < lockBitmap.Height; y++)
                {
                    if (lockBitmap.GetPixel(x, y).A > 0)
                    {
                        rect.Width = x;
                        ok = true;
                        break;
                    }
                }

                if (ok == true)
                {
                    break;
                }
            }

            //haut
            ok = false;
            for (int y = 0; y < lockBitmap.Height; y++)
            {
                for (int x = 0; x < lockBitmap.Width; x++)
                {
                    if (lockBitmap.GetPixel(x, y).A > 0)
                    {
                        rect.Y = y;
                        ok = true;
                        break;
                    }
                }

                if (ok == true)
                {
                    break;
                }
            }

            //bas
            ok = false;
            for (int y = lockBitmap.Height - 1; y >= rect.Y; y--)
            {
                for (int x = 0; x < lockBitmap.Width; x++)
                {
                    if (lockBitmap.GetPixel(x, y).A > 0)
                    {
                        rect.Height = y;
                        ok = true;
                        break;
                    }
                }

                if (ok == true)
                {
                    break;
                }
            }

            lockBitmap.UnlockBits();

            imageOrigins.Add(file_, new Point(bitmap.Width / 2 - rect.X, bitmap.Height / 2 - rect.Y));

            if (rect.X != 0
                || rect.Y != 0
                || rect.Width != bitmap.Width
                || rect.Height != bitmap.Height)
            {
                Bitmap b = ResizeBitmap(bitmap, rect);
                bitmap.Dispose();
                b.Save(file_, ImageFormat.Png);
                b.Dispose();
            }
            else
            {
                bitmap.Dispose();
            }

            return new Size(rect.Width - rect.X, rect.Height - rect.Y);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file_"></param>
        /// <param name="size_"></param>
        /// <param name="origin_"></param>
        /// <returns></returns>
        static public bool ShrinkBitmap(Bitmap bitmap_, out Rectangle rect_, out Point origin_)
        {
            if (bitmap_ == null)
                throw new ArgumentNullException("ImagePacker.ShrinkBitmap() : Bitmap is null");

            Rectangle rect = new Rectangle(0, 0, bitmap_.Width, bitmap_.Height);
            bool ok = false;

            LockBitmap lockBitmap = new LockBitmap(bitmap_);
            lockBitmap.LockBits();           

            //gauche
            for (int x = 0; x < lockBitmap.Width; x++)
            {
                for (int y = 0; y < lockBitmap.Height; y++)
                {
                    if (lockBitmap.GetPixel(x, y).A > 0)
                    {
                        rect.X = x;
                        ok = true;
                        break;
                    }
                }

                if (ok == true)
                {
                    break;
                }
            }

            //droite
            ok = false;
            for (int x = lockBitmap.Width - 1; x > rect.X; x--)
            {
                for (int y = 0; y < lockBitmap.Height; y++)
                {
                    if (lockBitmap.GetPixel(x, y).A > 0)
                    {
                        rect.Width = x;
                        ok = true;
                        break;
                    }
                }

                if (ok == true)
                {
                    break;
                }
            }

            //haut
            ok = false;
            for (int y = 0; y < lockBitmap.Height; y++)
            {
                for (int x = 0; x < lockBitmap.Width; x++)
                {
                    if (lockBitmap.GetPixel(x, y).A > 0)
                    {
                        rect.Y = y;
                        ok = true;
                        break;
                    }
                }

                if (ok == true)
                {
                    break;
                }
            }

            //bas
            ok = false;
            for (int y = lockBitmap.Height - 1; y >= rect.Y; y--)
            {
                for (int x = 0; x < lockBitmap.Width; x++)
                {
                    if (lockBitmap.GetPixel(x, y).A > 0)
                    {
                        rect.Height = y;
                        ok = true;
                        break;
                    }
                }

                if (ok == true)
                {
                    break;
                }
            }

            lockBitmap.UnlockBits();

            origin_ = new Point(bitmap_.Width / 2 - rect.X, bitmap_.Height / 2 - rect.Y);
            rect.Height -= rect.Y;
            rect.Width -= rect.X;
            rect_ = rect;

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="b"></param>
        /// <param name="size_"></param>
        /// <returns></returns>
        private Bitmap ResizeBitmap(Bitmap b, Rectangle size_)
        {
            Bitmap result = new Bitmap(size_.Width - size_.X, size_.Height - size_.Y);
            using (Graphics g = Graphics.FromImage((Image)result))
                g.DrawImage(b, 
                    new Rectangle(0, 0, size_.Width - size_.X, size_.Height - size_.Y),
                    size_.X, size_.Y, size_.Width - size_.X, size_.Height - size_.Y, GraphicsUnit.Pixel);
            return result;
        }

        /// <summary>
        /// call each time progress + 1
        /// </summary>
        private void ProgressChanged(string message)
        {
            if (OnProgressChanged != null)
            {
                OnProgressChanged.Invoke(message, EventArgs.Empty);
            }
        }
	}
}