
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using System.Xml.Linq;
using CasaEngine.Framework.UserInterface.Controls.Auxiliary;
using CasaEngine.Framework.UserInterface.Documents;

namespace CasaEngine.Framework.UserInterface
{

    public static class Layout
    {


        public static Container Load(string filename)
        {
            Container mainContainer = null;
            /*AssetContentManager userContentManager = AssetContentManager.CurrentContentManager;
            AssetContentManager temporalContent = new AssetContentManager { Name = "Temporal Content Manager", Hidden = true };
            AssetContentManager.CurrentContentManager = temporalContent;*/
            try
            {
                var layoutDocument = new Document("Layout\\" + filename);
                try
                {
                    if (layoutDocument.Resource.Element("Layout").Element("Controls") != null)
                    {
                        foreach (var control in layoutDocument.Resource.Element("Layout").Element("Controls").Elements())
                        {
                            var className = control.Attribute("Class").Value;
                            var type = Type.GetType("XNAFinalEngine.UserInterface." + className);
                            if (type == null)
                            {
                                throw new Exception("Failed to load layout: Control doesn't exist");
                            }
                            mainContainer = (Container)LoadControl(control, type, null);
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Failed to load layout: " + filename + ".\nThere are probably syntax errors.", e);
                }
            }
            finally
            {
                // Dispose the temporal content manager and restore the user content manager.
                //temporalContent.Dispose();
                //AssetContentManager.CurrentContentManager = userContentManager;
            }

            return mainContainer;
        } // Load



        private static Control LoadControl(XElement element, Type type, Control parent)
        {
            // Create control instance (it doesn't know the type in compiler time)
            var control = (Control)Activator.CreateInstance(type);

            if (parent != null)
            {
                control.Parent = parent;
            }

            control.Name = element.Attribute("Name").Value;
            // Load control's parameters
            if (element.Element("Properties") != null)
            {
                foreach (var property in element.Element("Properties").Elements())
                {
                    LoadProperty(property, control);
                }
            }
            // Load control's childrens
            if (element.Element("Controls") != null)
            {
                foreach (var subControl in element.Element("Controls").Elements())
                {
                    var className = subControl.Attribute("Class").Value;
                    var typeNewControl = Type.GetType("XNAFinalEngine.UserInterface." + className);
                    if (typeNewControl == null)
                    {
                        throw new Exception("Failed to load layout: Control doesn't exist");
                    }
                    LoadControl(subControl, typeNewControl, control);
                }
            }

            return control;
        } // LoadControl



        private static void LoadProperty(XElement property, Control control)
        {
            var name = property.Attribute("Name").Value;
            var val = property.Attribute("Value").Value;

            var i = control.GetType().GetProperty(name);

            if (i == null)
            {
                throw new Exception(string.Format("Failed to load layout: Parameter name {0} doesn't exist in control {1}", name, control.GetType().FullName));
            }
            try
            {
                i.SetValue(control, Convert.ChangeType(val, i.PropertyType, null), null);
            }
            catch
            {
                throw new Exception(string.Format("Failed to load layout: Parameter name {0} doesn't has a correct value", name));
            }
        } // LoadProperties


    } // Layout
} // XNAFinalEngine.UserInterface