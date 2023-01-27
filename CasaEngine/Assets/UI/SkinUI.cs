using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XNAFinalEngine.UserInterface;
using CasaEngine.Gameplay.Actor.Object;
using System.Xml;
using System.IO;
using CasaEngineCommon.Design;

#if EDITOR
using System.ComponentModel;
using Microsoft.Xna.Framework;
#endif

namespace CasaEngine.Assets.UI
{
    /// <summary>
    /// 
    /// </summary>
    public 
#if EDITOR
    partial
#endif
    class SkinUI
        : BaseObject
    {
        #region Fields

        SkinList<SkinControlInformation> m_Controls = new SkinList<SkinControlInformation>();        
        SkinList<SkinImage> m_Images = new SkinList<SkinImage>();        
        SkinList<SkinFont> m_Fonts = new SkinList<SkinFont>();
        SkinList<SkinCursor> m_Cursors = new SkinList<SkinCursor>();

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
#if EDITOR
        [Category("Skin")]
#endif
        public SkinList<SkinControlInformation> Controls
        {
            get { return m_Controls; }
        }

        /// <summary>
        /// 
        /// </summary>
#if EDITOR
        [Category("Skin")]
#endif
        public SkinList<SkinImage> Images
        {
            get { return m_Images; }
        }

        /// <summary>
        /// 
        /// </summary>
#if EDITOR
        [Category("Skin")]
#endif
        public SkinList<SkinFont> Fonts
        {
            get { return m_Fonts; }
        }

        /// <summary>
        /// 
        /// </summary>
#if EDITOR
        [Category("Skin")]
#endif
        public SkinList<SkinCursor> Cursors
        {
            get { return m_Cursors; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public SkinUI(XmlElement el_, SaveOption option_)
        {
            Load(el_, option_);
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override BaseObject Clone()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public override void Load(XmlElement el_, SaveOption option_)
        {
            base.Load(el_, option_);

            foreach (XmlNode controlNode in el_.SelectNodes("Skin/Controls/Control"))
            {
                XmlNode node;
                SkinControlInformation skinControl;
                Size size;
                Margins margin;
                string parent;
                bool inherit = false;

                // Create skin control
                parent = ReadAttribute(controlNode, "Inherits", null, false);
                inherit = false;
                if (parent != null) // If there is a parent then it loads the information from it.
                {
                    skinControl = new SkinControlInformation(Controls[parent]);
                    inherit = true;
                }
                else
                    skinControl = new SkinControlInformation();

                // Load general information
                string name = "";node = controlNode.SelectSingleNode("DefaultSize");
                ReadAttribute(ref name, inherit, controlNode, "Name", null, true);
                skinControl.Name = name;

                node = controlNode.SelectSingleNode("DefaultSize");
                size = new Size();                
                ReadAttribute(ref size.Width, inherit, node, "Width", 0, false);
                ReadAttribute(ref size.Height, inherit, node, "Height", 0, false);
                skinControl.DefaultSize = size;

                node = controlNode.SelectSingleNode("MinimumSize");
                ReadAttribute(ref size.Width, inherit, node, "Width", 0, false);
                ReadAttribute(ref size.Height, inherit, node, "Height", 0, false);
                skinControl.MinimumSize = size;

                node = controlNode.SelectSingleNode("OriginMargins");
                margin = new Margins();
                ReadAttribute(ref margin.Left, inherit, node, "Left", 0, false);
                ReadAttribute(ref margin.Top, inherit, node, "Top", 0, false);
                ReadAttribute(ref margin.Right, inherit, node, "Right", 0, false);
                ReadAttribute(ref margin.Bottom, inherit, node, "Bottom", 0, false);
                skinControl.OriginMargins = margin;

                node = controlNode.SelectSingleNode("ClientMargins");
                ReadAttribute(ref margin.Left, inherit, node, "Left", 0, false);
                ReadAttribute(ref margin.Top, inherit, node, "Top", 0, false);
                ReadAttribute(ref margin.Right, inherit, node, "Right", 0, false);
                ReadAttribute(ref margin.Bottom, inherit, node, "Bottom", 0, false);
                skinControl.ClientMargins = margin;

                node = controlNode.SelectSingleNode("ResizerSize");
                int resizerSize = 0;
                ReadAttribute(ref resizerSize, inherit, node, "Value", 0, false);
                skinControl.ResizerSize = resizerSize;

                // Load control's layers
                node = controlNode.SelectSingleNode("Layers");
                if (node != null)
                {
                    foreach (XmlNode layer in node.SelectNodes("Layer"))
                    {
                        LoadLayer(skinControl, layer);
                    }
                }
                Controls.Add(skinControl);
            }            

            foreach (XmlNode controlNode in el_.SelectNodes("Skin/Fonts/Font"))
            {
                SkinFont skinFont = new SkinFont
                {
                    Name = ReadAttribute(controlNode, "Name", null, true),
                    Filename = ReadAttribute(controlNode, "Asset", null, true)
                };
                Fonts.Add(skinFont);
            }

            foreach (XmlNode controlNode in el_.SelectNodes("Skin/Cursors/Cursor"))
            {
                SkinCursor skinCursor = new SkinCursor
                {
                    Name = ReadAttribute(controlNode, "Name", null, true),
                    Filename = ReadAttribute(controlNode, "Asset", null, true)
                };
                Cursors.Add(skinCursor);
            }

            foreach (XmlNode controlNode in el_.SelectNodes("Skin/Images/Image"))
            {
                SkinImage skinImage = new SkinImage
                {
                    Name = ReadAttribute(controlNode, "Name", null, true),
                    Filename = ReadAttribute(controlNode, "Asset", null, true)
                };
                Images.Add(skinImage);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="skinControl"></param>
        /// <param name="layerNode"></param>
        private void LoadLayer(SkinControlInformation skinControl, XmlNode layerNode)
        {
            string name = ReadAttribute(layerNode, "Name", null, true);
            bool over = ReadAttribute(layerNode, "Override", false, false);
            SkinLayer skinLayer = skinControl.Layers[name];

            bool inherent = true;
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

            Color color = new Color();
            int integer = 0;
            bool boolean = true;
            Margins margin = new Margins();
            XmlNode node;

            ReadAttribute(ref name, inherent, layerNode, "Name", null, true);
            skinLayer.Name = name;
            ReadAttribute(ref name, inherent, layerNode, "Image", "Control", false);
            skinLayer.Image.Name = name;
            ReadAttribute(ref integer, inherent, layerNode, "Width", 0, false);
            skinLayer.Width = integer;
            ReadAttribute(ref integer, inherent, layerNode, "Height", 0, false);
            skinLayer.Height = integer;

            string layerAlignment = skinLayer.Alignment.ToString();
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

            #region States

            node = layerNode.SelectSingleNode("States");
            if (node != null)
            {
                SkinStates<LayerStates> states = new SkinStates<LayerStates>();

                ReadAttribute(ref integer, inherent, node.SelectSingleNode("Enabled"), "Index", 0, false);
                states.Enabled.Index = integer;
                int di = skinLayer.States.Enabled.Index;
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
                Color dc = skinLayer.States.Enabled.Color;
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
                bool dv = skinLayer.States.Enabled.Overlay;
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

            #endregion

            #region Overlays

            node = layerNode.SelectSingleNode("Overlays");
            if (node != null)
            {
                SkinStates<LayerOverlays> overlay = new SkinStates<LayerOverlays>();

                ReadAttribute(ref integer, inherent, node.SelectSingleNode("Enabled"), "Index", 0, false);
                overlay.Enabled.Index = integer;
                int di = skinLayer.Overlays.Enabled.Index;
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
                Color dc = skinLayer.Overlays.Enabled.Color;
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

            #endregion

            #region Text

            node = layerNode.SelectSingleNode("Text");
            if (node != null)
            {
                SkinText skinText = new SkinText();

                ReadAttribute(ref name, inherent, node, "Font", null, true);
                skinText.Name = name;
                ReadAttribute(ref integer, inherent, node, "OffsetX", 0, false);
                skinText.OffsetX = integer;
                ReadAttribute(ref integer, inherent, node, "OffsetY", 0, false);
                skinText.OffsetY = integer;

                layerAlignment = skinLayer.Text.Alignment.ToString();
                ReadAttribute(ref layerAlignment, inherent, node, "Alignment", "MiddleCenter", false);
                skinLayer.Text.Alignment = (Alignment)Enum.Parse(typeof(Alignment), layerAlignment, true);

                SkinStates<Color> colors = new SkinStates<Color>();
                LoadColors(inherent, node, ref colors);
                skinLayer.Text.Colors = colors;

                skinLayer.Text = skinText;
            }

            #endregion

            #region Attributes

            foreach (XmlNode attribute in layerNode.SelectNodes("Attributes/Attribute"))
            {
                LoadLayerAttribute(skinLayer, attribute);
            }

            #endregion

            if (!inherent)
                skinControl.Layers.Add(skinLayer);
        } // LoadLayer

        #region Load Colors

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

        #endregion

        #region Load Layer Attributes

        /// <summary>
        /// Load Layer Attributes
        /// </summary>
        private void LoadLayerAttribute(SkinLayer skinLayer, XmlNode e)
        {
            string name = ReadAttribute(e, "Name", null, true);
            SkinAttribute skinAttribute = skinLayer.Attributes[name];
            bool inherent = true;

            if (skinAttribute == null)
            {
                skinAttribute = new SkinAttribute();
                inherent = false;
            }

            skinAttribute.Name = name;
            ReadAttribute(ref name, inherent, e, "Value", null, true);
            skinAttribute.Value = name;

            if (!inherent)
                skinLayer.Attributes.Add(skinAttribute);

        } // LoadLayerAttribute

        #endregion

        #region Read Attribute

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
            string tmp = retval.ToString();
            ReadAttribute(ref tmp, inherited, element, attrib, defval.ToString(), needed);
            retval = int.Parse(tmp);
        } // ReadAttributeInt

        private bool ReadAttribute(XmlNode element, string attrib, bool defval, bool needed)
        {
            return bool.Parse(ReadAttribute(element, attrib, defval.ToString(), needed));
        } // ReadAttributeBool

        private void ReadAttribute(ref bool retval, bool inherited, XmlNode element, string attrib, bool defval, bool needed)
        {
            string tmp = retval.ToString();
            ReadAttribute(ref tmp, inherited, element, attrib, defval.ToString(), needed);
            retval = bool.Parse(tmp);
        } // ReadAttributeBool

        private string ColorToString(Color c)
        {
            return string.Format("{0};{1};{2};{3}", c.R, c.G, c.B, c.A);
        } // ColorToString

        private void ReadAttribute(ref Color retval, bool inherited, XmlNode element, string attrib, Color defval, bool needed)
        {
            string tmp = ColorToString(retval);
            ReadAttribute(ref tmp, inherited, element, attrib, ColorToString(defval), needed);
            retval = Utilities.ParseColor(tmp);
        } // ReadAttributeColor

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="br_"></param>
        /// <param name="option_"></param>
        public override void Load(BinaryReader br_, SaveOption option_)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        protected override void CopyFrom(BaseObject ob_)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
