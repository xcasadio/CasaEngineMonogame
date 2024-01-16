using System.Xml;
using System.Reflection;

namespace TomShane.Neoforce.Controls;

public static class Layout
{
    public static Container Load(Manager manager, string asset)
    {
        Container win = null;
        var doc = new LayoutXmlDocument();
        var content = new ArchiveManager(manager.Game.Services);

        try
        {
            content.RootDirectory = manager.LayoutDirectory;

            var file = content.RootDirectory + asset;

            if (File.Exists(file))
            {
                doc.Load(file);
            }
            else
            {
                doc = content.Load<LayoutXmlDocument>(asset);
            }

            if (doc?["Layout"]["Controls"] != null && doc["Layout"]["Controls"].HasChildNodes)
            {
                var node = doc["Layout"]["Controls"].GetElementsByTagName("Control").Item(0);
                var cls = node.Attributes["Class"].Value;
                var type = Type.GetType(cls);

                if (type == null)
                {
                    cls = "TomShane.Neoforce.Controls." + cls;
                    type = Type.GetType(cls);
                }

                win = (Container)LoadControl(manager, node, type, null);
            }

        }
        finally
        {
            content.Dispose();
        }

        return win;
    }

    private static Control LoadControl(Manager manager, XmlNode node, Type type, Control parent)
    {
        Control c = null;

        var args = new Object[] { manager };

        c = (Control)type.InvokeMember(null, BindingFlags.CreateInstance, null, null, args);
        if (parent != null)
        {
            c.Parent = parent;
        }

        c.Name = node.Attributes["Name"].Value;

        if (node?["Properties"] != null && node["Properties"].HasChildNodes)
        {
            LoadProperties(node["Properties"].GetElementsByTagName("Property"), c);
        }

        if (node?["Controls"] != null && node["Controls"].HasChildNodes)
        {
            foreach (XmlElement e in node["Controls"].GetElementsByTagName("Control"))
            {
                var cls = e.Attributes["Class"].Value;
                var t = Type.GetType(cls);

                if (t == null)
                {
                    cls = "TomShane.Neoforce.Controls." + cls;
                    t = Type.GetType(cls);
                }
                LoadControl(manager, e, t, c);
            }
        }

        return c;
    }

    private static void LoadProperties(XmlNodeList node, Control c)
    {
        foreach (XmlElement e in node)
        {
            var name = e.Attributes["Name"].Value;
            var val = e.Attributes["Value"].Value;

            var i = c.GetType().GetProperty(name);

            if (i != null)
            {
                i.SetValue(c, Convert.ChangeType(val, i.PropertyType, null), null);
            }
        }
    }
}