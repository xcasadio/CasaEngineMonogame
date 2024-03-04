using Microsoft.Xna.Framework.Content;

namespace CasaEngine.Framework.GUI.Neoforce;

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