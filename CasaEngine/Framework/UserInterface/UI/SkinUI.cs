using System.Text.Json;
using System.Xml;
using CasaEngine.Framework.Entities;
using CasaEngine.Core.Design;
using Size = CasaEngine.Core.Maths.Size;

#if EDITOR
using System.ComponentModel;
#endif

namespace CasaEngine.Framework.UserInterface.UI
{
    public class SkinUi
    {
        private readonly SkinList<SkinControlInformation> _controls = new();
        private readonly SkinList<SkinImage> _images = new();
        private readonly SkinList<SkinFont> _fonts = new();
        private readonly SkinList<SkinCursor> _cursors = new();

#if EDITOR
        [Category("Skin")]
#endif
        public SkinList<SkinControlInformation> Controls
        {
            get { return _controls; }
        }

#if EDITOR
        [Category("Skin")]
#endif
        public SkinList<SkinImage> Images
        {
            get { return _images; }
        }

#if EDITOR
        [Category("Skin")]
#endif
        public SkinList<SkinFont> Fonts
        {
            get { return _fonts; }
        }

#if EDITOR
        [Category("Skin")]
#endif
        public SkinList<SkinCursor> Cursors
        {
            get { return _cursors; }
        }

        public SkinUi(JsonElement element, SaveOption option)
        {
            Load(element, option);
        }

        public Entity Clone()
        {
            throw new NotImplementedException();
        }

        public virtual void Load(JsonElement element, SaveOption option)
        {
            //base.Load(el, option);

            foreach (var controlNode in element.GetProperty("controls/").EnumerateArray())
            {
                SkinControlInformation skinControl;
                Size size;
                Margins margin;
                string parent;
                var inherit = false;

                // Create skin control
                parent = controlNode.GetProperty("inherits").GetString();
                inherit = false;
                if (parent != null) // If there is a parent then it loads the information from it.
                {
                    skinControl = new SkinControlInformation(Controls[parent]);
                    inherit = true;
                }
                else
                {
                    skinControl = new SkinControlInformation();
                }

                // Load general information
                //var name = ""; 
                //node = controlNode.SelectSingleNode("DefaultSize");
                //ReadAttribute(ref name, inherit, controlNode, "Name", null, true);
                //skinControl.Name = name;
                //
                //node = controlNode.SelectSingleNode("DefaultSize");
                //size = new Size();
                //ReadAttribute(ref size.Width, inherit, node, "Width", 0, false);
                //ReadAttribute(ref size.Height, inherit, node, "Height", 0, false);
                //skinControl.DefaultSize = size;
                //
                //node = controlNode.SelectSingleNode("MinimumSize");
                //ReadAttribute(ref size.Width, inherit, node, "Width", 0, false);
                //ReadAttribute(ref size.Height, inherit, node, "Height", 0, false);
                //skinControl.MinimumSize = size;
                //
                //node = controlNode.SelectSingleNode("OriginMargins");
                //margin = new Margins();
                //ReadAttribute(ref margin.Left, inherit, node, "Left", 0, false);
                //ReadAttribute(ref margin.Top, inherit, node, "Top", 0, false);
                //ReadAttribute(ref margin.Right, inherit, node, "Right", 0, false);
                //ReadAttribute(ref margin.Bottom, inherit, node, "Bottom", 0, false);
                //skinControl.OriginMargins = margin;
                //
                //node = controlNode.SelectSingleNode("ClientMargins");
                //ReadAttribute(ref margin.Left, inherit, node, "Left", 0, false);
                //ReadAttribute(ref margin.Top, inherit, node, "Top", 0, false);
                //ReadAttribute(ref margin.Right, inherit, node, "Right", 0, false);
                //ReadAttribute(ref margin.Bottom, inherit, node, "Bottom", 0, false);
                //skinControl.ClientMargins = margin;
                //
                //node = controlNode.SelectSingleNode("ResizerSize");
                //var resizerSize = 0;
                //ReadAttribute(ref resizerSize, inherit, node, "Value", 0, false);
                //skinControl.ResizerSize = resizerSize;
                //
                //// Load control's layers
                //node = controlNode.SelectSingleNode("Layers");
                //if (node != null)
                //{
                //    foreach (XmlNode layer in node.SelectNodes("Layer"))
                //    {
                //        LoadLayer(skinControl, layer);
                //    }
                //}
                Controls.Add(skinControl);
            }

            //foreach (XmlNode controlNode in el.SelectNodes("Skin/Fonts/Font"))
            //{
            //    var skinFont = new SkinFont
            //    {
            //        Name = ReadAttribute(controlNode, "Name", null, true),
            //        Filename = ReadAttribute(controlNode, "Asset", null, true)
            //    };
            //    Fonts.Add(skinFont);
            //}
            //
            //foreach (XmlNode controlNode in el.SelectNodes("Skin/Cursors/Cursor"))
            //{
            //    var skinCursor = new SkinCursor
            //    {
            //        Name = ReadAttribute(controlNode, "Name", null, true),
            //        Filename = ReadAttribute(controlNode, "Asset", null, true)
            //    };
            //    Cursors.Add(skinCursor);
            //}
            //
            //foreach (XmlNode controlNode in el.SelectNodes("Skin/Images/Image"))
            //{
            //    var skinImage = new SkinImage
            //    {
            //        Name = ReadAttribute(controlNode, "Name", null, true),
            //        Filename = ReadAttribute(controlNode, "Asset", null, true)
            //    };
            //    Images.Add(skinImage);
            //}
        }

        private void LoadLayer(SkinControlInformation skinControl, XmlNode layerNode)
        {
            var name = ReadAttribute(layerNode, "Name", null, true);
            var over = ReadAttribute(layerNode, "Override", false, false);
            var skinLayer = skinControl.Layers[name];

            var inherent = true;
            if (skinLayer == null)
            {
                skinLayer = new SkinLayer();
                inherent = false;
            }

            if (inherent && over)
            {
                skinLayer = new SkinLayer();
                skinControl.Layers[name] = skinLayer;
            }

            var color = new Color();
            var integer = 0;
            var boolean = true;
            var margin = new Margins();
            XmlNode node;

            ReadAttribute(ref name, inherent, layerNode, "Name", null, true);
            skinLayer.Name = name;
            ReadAttribute(ref name, inherent, layerNode, "Image", "Control", false);
            skinLayer.Image.Name = name;
            ReadAttribute(ref integer, inherent, layerNode, "Width", 0, false);
            skinLayer.Width = integer;
            ReadAttribute(ref integer, inherent, layerNode, "Height", 0, false);
            skinLayer.Height = integer;

            var layerAlignment = skinLayer.Alignment.ToString();
            ReadAttribute(ref layerAlignment, inherent, layerNode, "Alignment", "MiddleCenter", false);
            skinLayer.Alignment = (Alignment)Enum.Parse(typeof(Alignment), layerAlignment, true);

            ReadAttribute(ref integer, inherent, layerNode, "OffsetX", 0, false);
            skinLayer.OffsetX = integer;
            ReadAttribute(ref integer, inherent, layerNode, "OffsetY", 0, false);
            skinLayer.OffsetY = integer;

            node = layerNode.SelectSingleNode("SizingMargins");
            ReadAttribute(ref margin.Left, inherent, node, "Left", 0, false);
            ReadAttribute(ref margin.Top, inherent, node, "Top", 0, false);
            ReadAttribute(ref margin.Right, inherent, node, "Right", 0, false);
            ReadAttribute(ref margin.Bottom, inherent, node, "Bottom", 0, false);
            skinLayer.SizingMargins = margin;

            node = layerNode.SelectSingleNode("ContentMargins");
            ReadAttribute(ref margin.Left, inherent, node, "Left", 0, false);
            ReadAttribute(ref margin.Top, inherent, node, "Top", 0, false);
            ReadAttribute(ref margin.Right, inherent, node, "Right", 0, false);
            ReadAttribute(ref margin.Bottom, inherent, node, "Bottom", 0, false);
            skinLayer.ContentMargins = margin;

            node = layerNode.SelectSingleNode("States");
            if (node != null)
            {
                var states = new SkinStates<LayerStates>();

                ReadAttribute(ref integer, inherent, node.SelectSingleNode("Enabled"), "Index", 0, false);
                states.Enabled.Index = integer;
                var di = skinLayer.States.Enabled.Index;
                ReadAttribute(ref states.Hovered.Index, inherent, node.SelectSingleNode("Hovered"), "Index", di, false);
                states.Hovered.Index = integer;
                ReadAttribute(ref states.Pressed.Index, inherent, node.SelectSingleNode("Pressed"), "Index", di, false);
                states.Pressed.Index = integer;
                ReadAttribute(ref states.Focused.Index, inherent, node.SelectSingleNode("Focused"), "Index", di, false);
                states.Focused.Index = integer;
                ReadAttribute(ref states.Disabled.Index, inherent, node.SelectSingleNode("Disabled"), "Index", di, false);
                states.Disabled.Index = integer;

                ReadAttribute(ref color, inherent, node.SelectSingleNode("Enabled"), "Color", Color.White, false);
                states.Enabled.Color = color;
                var dc = skinLayer.States.Enabled.Color;
                ReadAttribute(ref color, inherent, node.SelectSingleNode("Hovered"), "Color", dc, false);
                states.Hovered.Color = color;
                ReadAttribute(ref color, inherent, node.SelectSingleNode("Pressed"), "Color", dc, false);
                states.Pressed.Color = color;
                ReadAttribute(ref color, inherent, node.SelectSingleNode("Focused"), "Color", dc, false);
                states.Focused.Color = color;
                ReadAttribute(ref color, inherent, node.SelectSingleNode("Disabled"), "Color", dc, false);
                states.Disabled.Color = color;

                ReadAttribute(ref boolean, inherent, node.SelectSingleNode("Enabled"), "Overlay", false, false);
                states.Enabled.Overlay = boolean;
                var dv = skinLayer.States.Enabled.Overlay;
                ReadAttribute(ref boolean, inherent, node.SelectSingleNode("Hovered"), "Overlay", dv, false);
                states.Hovered.Overlay = boolean;
                ReadAttribute(ref boolean, inherent, node.SelectSingleNode("Pressed"), "Overlay", dv, false);
                states.Pressed.Overlay = boolean;
                ReadAttribute(ref boolean, inherent, node.SelectSingleNode("Focused"), "Overlay", dv, false);
                states.Focused.Overlay = boolean;
                ReadAttribute(ref boolean, inherent, node.SelectSingleNode("Disabled"), "Overlay", dv, false);
                states.Disabled.Overlay = boolean;

                skinLayer.States = states;
            }

            node = layerNode.SelectSingleNode("Overlays");
            if (node != null)
            {
                var overlay = new SkinStates<LayerOverlays>();

                ReadAttribute(ref integer, inherent, node.SelectSingleNode("Enabled"), "Index", 0, false);
                overlay.Enabled.Index = integer;
                var di = skinLayer.Overlays.Enabled.Index;
                ReadAttribute(ref overlay.Hovered.Index, inherent, node.SelectSingleNode("Hovered"), "Index", di, false);
                overlay.Hovered.Index = integer;
                ReadAttribute(ref overlay.Pressed.Index, inherent, node.SelectSingleNode("Pressed"), "Index", di, false);
                overlay.Pressed.Index = integer;
                ReadAttribute(ref overlay.Focused.Index, inherent, node.SelectSingleNode("Focused"), "Index", di, false);
                overlay.Focused.Index = integer;
                ReadAttribute(ref overlay.Disabled.Index, inherent, node.SelectSingleNode("Disabled"), "Index", di, false);
                overlay.Disabled.Index = integer;

                ReadAttribute(ref overlay.Enabled.Color, inherent, node.SelectSingleNode("Enabled"), "Color", Color.White, false);
                overlay.Enabled.Color = color;
                var dc = skinLayer.Overlays.Enabled.Color;
                ReadAttribute(ref overlay.Hovered.Color, inherent, node.SelectSingleNode("Hovered"), "Color", dc, false);
                overlay.Hovered.Color = color;
                ReadAttribute(ref overlay.Pressed.Color, inherent, node.SelectSingleNode("Pressed"), "Color", dc, false);
                overlay.Pressed.Color = color;
                ReadAttribute(ref overlay.Focused.Color, inherent, node.SelectSingleNode("Focused"), "Color", dc, false);
                overlay.Focused.Color = color;
                ReadAttribute(ref overlay.Disabled.Color, inherent, node.SelectSingleNode("Disabled"), "Color", dc, false);
                overlay.Disabled.Color = color;

                skinLayer.Overlays = overlay;
            }

            node = layerNode.SelectSingleNode("Text");
            if (node != null)
            {
                var skinText = new SkinText();

                ReadAttribute(ref name, inherent, node, "Font", null, true);
                skinText.Name = name;
                ReadAttribute(ref integer, inherent, node, "OffsetX", 0, false);
                skinText.OffsetX = integer;
                ReadAttribute(ref integer, inherent, node, "OffsetY", 0, false);
                skinText.OffsetY = integer;

                layerAlignment = skinLayer.Text.Alignment.ToString();
                ReadAttribute(ref layerAlignment, inherent, node, "Alignment", "MiddleCenter", false);
                skinLayer.Text.Alignment = (Alignment)Enum.Parse(typeof(Alignment), layerAlignment, true);

                var colors = new SkinStates<Color>();
                LoadColors(inherent, node, ref colors);
                skinLayer.Text.Colors = colors;

                skinLayer.Text = skinText;
            }

            foreach (XmlNode attribute in layerNode.SelectNodes("Attributes/Attribute"))
            {
                LoadLayerAttribute(skinLayer, attribute);
            }

            if (!inherent)
            {
                skinControl.Layers.Add(skinLayer);
            }
        } // LoadLayer

        private void LoadColors(bool inherited, XmlNode e, ref SkinStates<Color> colors)
        {
            if (e != null)
            {
                ReadAttribute(ref colors.Enabled, inherited, e.SelectSingleNode("Colors/Enabled"), "Color", Color.White, false);
                ReadAttribute(ref colors.Hovered, inherited, e.SelectSingleNode("Colors/Hovered"), "Color", colors.Enabled, false);
                ReadAttribute(ref colors.Pressed, inherited, e.SelectSingleNode("Colors/Pressed"), "Color", colors.Enabled, false);
                ReadAttribute(ref colors.Focused, inherited, e.SelectSingleNode("Colors/Focused"), "Color", colors.Enabled, false);
                ReadAttribute(ref colors.Disabled, inherited, e.SelectSingleNode("Colors/Disabled"), "Color", colors.Enabled, false);
            }
        } // LoadColors

        private void LoadLayerAttribute(SkinLayer skinLayer, XmlNode e)
        {
            var name = ReadAttribute(e, "Name", null, true);
            var skinAttribute = skinLayer.Attributes[name];
            var inherent = true;

            if (skinAttribute == null)
            {
                skinAttribute = new SkinAttribute();
                inherent = false;
            }

            skinAttribute.Name = name;
            ReadAttribute(ref name, inherent, e, "Value", null, true);
            skinAttribute.Value = name;

            if (!inherent)
            {
                skinLayer.Attributes.Add(skinAttribute);
            }
        } // LoadLayerAttribute

        private string ReadAttribute(XmlNode element, string attributeName, string defval, bool needed)
        {
            if (element != null && element.Attributes[attributeName] != null)
            {
                return element.Attributes[attributeName].Value;
            }
            if (needed)
            {
                throw new Exception("Missing required attribute \"" + attributeName + "\" in the skin file.");
            }
            return defval;
        } // ReadAttribute

        private void ReadAttribute(ref string retval, bool inherited, XmlNode element, string attributeName, string defaultValue, bool needed)
        {
            if (element != null && element.Attributes[attributeName] != null)
            {
                retval = element.Attributes[attributeName].Value;
            }
            else if (inherited)
            {
                // Do nothing, the parent has the attribute.
            }
            else if (needed)
            {
                throw new Exception("Missing required attribute \"" + attributeName + "\" in the skin file.");
            }
            else
            {
                retval = defaultValue;
            }
        } // ReadAttribute

        private void ReadAttribute(ref int retval, bool inherited, XmlNode element, string attrib, int defval, bool needed)
        {
            var tmp = retval.ToString();
            ReadAttribute(ref tmp, inherited, element, attrib, defval.ToString(), needed);
            retval = int.Parse(tmp);
        } // ReadAttributeInt

        private bool ReadAttribute(XmlNode element, string attrib, bool defval, bool needed)
        {
            return bool.Parse(ReadAttribute(element, attrib, defval.ToString(), needed));
        } // ReadAttributeBool

        private void ReadAttribute(ref bool retval, bool inherited, XmlNode element, string attrib, bool defval, bool needed)
        {
            var tmp = retval.ToString();
            ReadAttribute(ref tmp, inherited, element, attrib, defval.ToString(), needed);
            retval = bool.Parse(tmp);
        } // ReadAttributeBool

        private string ColorToString(Color c)
        {
            return string.Format("{0};{1};{2};{3}", c.R, c.G, c.B, c.A);
        } // ColorToString

        private void ReadAttribute(ref Color retval, bool inherited, XmlNode element, string attrib, Color defval, bool needed)
        {
            var tmp = ColorToString(retval);
            ReadAttribute(ref tmp, inherited, element, attrib, ColorToString(defval), needed);
            retval = Utilities.ParseColor(tmp);
        } // ReadAttributeColor
    }
}
