using Microsoft.Xna.Framework.Content;

namespace CasaEngine.Framework.GUI.Neoforce;

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