using System.Xml;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework;

namespace TomShane.Neoforce.Controls;

public class LayoutXmlDocument : XmlDocument { }
public class SkinXmlDocument : XmlDocument { }

public class SkinReader : ContentTypeReader<SkinXmlDocument>
{
    protected override SkinXmlDocument Read(ContentReader input, SkinXmlDocument existingInstance)
    {
        if (existingInstance == null)
        {
            var doc = new SkinXmlDocument();
            doc.LoadXml(input.ReadString());
            return doc;
        }

        existingInstance.LoadXml(input.ReadString());

        return existingInstance;
    }
}

public class LayoutReader : ContentTypeReader<LayoutXmlDocument>
{
    protected override LayoutXmlDocument Read(ContentReader input, LayoutXmlDocument existingInstance)
    {
        if (existingInstance == null)
        {
            var doc = new LayoutXmlDocument();
            doc.LoadXml(input.ReadString());
            return doc;
        }

        existingInstance.LoadXml(input.ReadString());

        return existingInstance;
    }
}

public class CursorReader : ContentTypeReader<Cursor>
{
    protected override Cursor Read(ContentReader input, Cursor existingInstance)
    {
        if (existingInstance == null)
        {
            var count = input.ReadInt32();
            var data = input.ReadBytes(count);

            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, data);
            var tPath = Path.GetTempFileName();
            using (var i = System.Drawing.Icon.ExtractAssociatedIcon(path))
            {
                using (var b = i.ToBitmap())
                {

                    b.Save(tPath, System.Drawing.Imaging.ImageFormat.Png);
                    b.Dispose();
                }

                i.Dispose();
            }
            //TODO: Replace with xml based solution for getting hotspot and size instead
            var handle = NativeMethods.LoadCursor(path);
            var c = new System.Windows.Forms.Cursor(handle);
            var hs = new Vector2(c.HotSpot.X, c.HotSpot.Y);
            var w = c.Size.Width;
            var h = c.Size.Height;
            c.Dispose();
            File.Delete(path);

            return new Cursor(tPath, hs, w, h);
        }

        return existingInstance;
    }
}