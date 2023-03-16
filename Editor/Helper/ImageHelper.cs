namespace Editor.Helper
{
    public static class ImageHelper
    {
        public static Image LoadImageFromFileAndRelease(string file)
        {
            FileStream stream = new(file, FileMode.Open, FileAccess.Read);
            var img = Image.FromStream(stream);
            stream.Close();
            return img;
        }
    }
}
