
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using System.Xml.Linq;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.UserInterface.Documents;
using Cursor = CasaEngine.Framework.UserInterface.Cursors.Cursor;
using Size = CasaEngine.Core.Size;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;


namespace CasaEngine.Framework.UserInterface;

public struct SkinStates<T>
{
    public T Enabled;
    public T Hovered;
    public T Pressed;
    public T Focused;
    public T Disabled;

    public SkinStates(T enabled, T hovered, T pressed, T focused, T disabled)
    {
        Enabled = enabled;
        Hovered = hovered;
        Pressed = pressed;
        Focused = focused;
        Disabled = disabled;
    } // SkinStates

} // SkinStates

public struct LayerStates
{
    public int Index;
    public Color Color;
    public bool Overlay;
} // LayerStates

public struct LayerOverlays
{
    public int Index;
    public Color Color;
} // LayerOverlays



public class SkinList<T> : List<T>
{


    public T this[string index]
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                var s = (SkinBase)(object)this[i];
                //if (s.Name.ToLower() == index.ToLower()) // Not need to produce so much garbage unnecessary.
                if (s.Name == index)
                {
                    return this[i];
                }
            }
            return default;
        }
        set
        {
            for (var i = 0; i < Count; i++)
            {
                var s = (SkinBase)(object)this[i];
                //if (s.Name.ToLower() == index.ToLower())
                if (s.Name == index)
                {
                    this[i] = value;
                }
            }
        }
    } // this



    public SkinList() { }

    public SkinList(SkinList<T> source)
    {
        foreach (var t1 in source)
        {
            var t = new Type[1];
            t[0] = typeof(T);

            var p = new object[1];
            p[0] = t1;

            Add((T)t[0].GetConstructor(t).Invoke(p));
        }
    } // SkinList


} // SkinList



public class SkinBase
{


    public string Name
    {
        get;
        set;
    }



    public SkinBase() { }

    public SkinBase(SkinBase source)
    {
        if (source != null)
        {
            Name = source.Name;
        }
    } // SkinBase


} // SkinBase



public class SkinLayer : SkinBase
{


    public SkinImage Image
    {
        get;
        set;
    }

    public int Width
    {
        get;
        set;
    }

    public int Height
    {
        get;
        set;
    }

    public int OffsetX
    {
        get;
        set;
    }

    public int OffsetY
    {
        get;
        set;
    }

    public Alignment Alignment
    {
        get;
        set;
    }

    public Margins SizingMargins
    {
        get;
        set;
    }

    public Margins ContentMargins
    {
        get;
        set;
    }

    public SkinStates<LayerStates> States
    {
        get;
        set;
    }

    public SkinStates<LayerOverlays> Overlays
    {
        get;
        set;
    }

    public SkinText Text
    {
        get;
        set;
    }

    public SkinList<SkinAttribute> Attributes
    {
        get;
        set;
    }




    public SkinLayer()
    {
        var states = new SkinStates<LayerStates>();

        states.Enabled.Color = Color.White;
        states.Pressed.Color = Color.White;
        states.Focused.Color = Color.White;
        states.Hovered.Color = Color.White;
        states.Disabled.Color = Color.White;
        States = states;

        var overlays = new SkinStates<LayerOverlays>();
        overlays.Enabled.Color = Color.White;
        overlays.Pressed.Color = Color.White;
        overlays.Focused.Color = Color.White;
        overlays.Hovered.Color = Color.White;
        overlays.Disabled.Color = Color.White;
        Overlays = overlays;

        Text = new SkinText();
        Attributes = new SkinList<SkinAttribute>();
        Image = new SkinImage();
    } // SkinLayer

    public SkinLayer(SkinLayer source) : base(source)
    {
        if (source != null)
        {
            Image = new SkinImage(source.Image);
            Width = source.Width;
            Height = source.Height;
            OffsetX = source.OffsetX;
            OffsetY = source.OffsetY;
            Alignment = source.Alignment;
            SizingMargins = source.SizingMargins;
            ContentMargins = source.ContentMargins;
            States = source.States;
            Overlays = source.Overlays;
            Text = new SkinText(source.Text);
            Attributes = new SkinList<SkinAttribute>(source.Attributes);
        }
        else
        {
            throw new Exception("Parameter for SkinLayer copy constructor cannot be null.");
        }
    } // SkinLayer


} // SkinLayer



public class SkinText : SkinBase
{


    public SkinFont Font
    {
        get;
        set;
    }

    public int OffsetX
    {
        get;
        set;
    }


    public int OffsetY
    {
        get;
        set;
    }


    public Alignment Alignment
    {
        get;
        set;
    }


    public SkinStates<Color> Colors
    {
        get;
        set;
    }



    public SkinText()
    {
        var colors = new SkinStates<Color>();
        colors.Enabled = Color.White;
        colors.Pressed = Color.White;
        colors.Focused = Color.White;
        colors.Hovered = Color.White;
        colors.Disabled = Color.White;
        Colors = colors;
    } // SkinText

    public SkinText(SkinText source) : base(source)
    {
        if (source != null)
        {
            Font = new SkinFont(source.Font);
            OffsetX = source.OffsetX;
            OffsetY = source.OffsetY;
            Alignment = source.Alignment;
            Colors = source.Colors;
        }
    } // SkinText


} // SkinText



public class SkinFont : SkinBase
{


    public Font Font;

    public string Filename
    {
        get;
        set;
    }



    public int Height
    {
        get
        {
            if (Font != null)
            {
                return (int)Font.MeasureString("AaYy").Y;
            }
            return 0;
        }
    } // Height



    public SkinFont() { }

    public SkinFont(SkinFont source) : base(source)
    {
        if (source != null)
        {
            Font = source.Font;
            Filename = source.Filename;
        }
    } // SkinFont


} // SkinFont



public class SkinImage : SkinBase
{


    public Texture Texture;

    public string Filename
    {
        get;
        set;
    }



    public SkinImage() { }

    public SkinImage(SkinImage source) : base(source)
    {
        Texture = source.Texture;
        Filename = source.Filename;
    } // SkinImage


} // SkinImage



#if (WINDOWS)

public class SkinCursor : SkinBase
{

    public Cursor Cursor;

    public string Filename
    {
        get;
        set;
    }
} // SkinCursor

#endif



public class SkinControlInformation : SkinBase
{


    public Size DefaultSize
    {
        get;
        set;
    }

    public int ResizerSize
    {
        get;
        set;
    }

    public Size MinimumSize
    {
        get;
        set;
    }

    public Margins OriginMargins
    {
        get;
        set;
    }

    public Margins ClientMargins
    {
        get;
        set;
    }

    public SkinList<SkinLayer> Layers
    {
        get;
        private set;
    }

    public SkinList<SkinAttribute> Attributes
    {
        get;
        private set;
    }



    public SkinControlInformation()
    {
        Layers = new SkinList<SkinLayer>();
        Attributes = new SkinList<SkinAttribute>();
    }

    public SkinControlInformation(SkinControlInformation source) : base(source)
    {
        DefaultSize = source.DefaultSize;
        MinimumSize = source.MinimumSize;
        OriginMargins = source.OriginMargins;
        ClientMargins = source.ClientMargins;
        ResizerSize = source.ResizerSize;
        Layers = new SkinList<SkinLayer>(source.Layers);
        Attributes = new SkinList<SkinAttribute>(source.Attributes);
    } // SkinControl


} // SkinControl



public class SkinAttribute : SkinBase
{


    public string Value
    {
        get;
        set;
    }



    public SkinAttribute() { }

    public SkinAttribute(SkinAttribute source) : base(source)
    {
        Value = source.Value;
    } // SkinAttribute


} // SkinAttribute



public class Skin
{


    private Document _skinDescription;

    //private AssetContentManager skinContentManager;

    private static readonly string SkinContentManagerCategory = "UserInterfaceSkin";



    public string CurrentSkinName { get; private set; }

    public SkinList<SkinControlInformation> Controls { get; private set; }

    public SkinList<SkinFont> Fonts { get; private set; }

#if (WINDOWS)
    public SkinList<SkinCursor> Cursors { get; private set; }
#endif

    public SkinList<SkinImage> Images { get; private set; }



    public void LoadSkin(CasaEngineGame game, string skinName)
    {
        CurrentSkinName = skinName;

        //AssetContentManager userContentManager = AssetContentManager.CurrentContentManager;


        Controls = new SkinList<SkinControlInformation>();
        Fonts = new SkinList<SkinFont>();
        Images = new SkinList<SkinImage>();
#if (WINDOWS)
        Cursors = new SkinList<SkinCursor>();
#endif

        /*if (skinContentManager == null)
            skinContentManager = new AssetContentManager { Name = "Skin Content Manager", Hidden = true };
        else
            skinContentManager.Unload();*/
        /*if (skinContentManager != null)
        {
            skinContentManager.Unload(skinContentManagerCategory);
        }*/
        game.GameManager.AssetContentManager.Unload(SkinContentManagerCategory);



        var fullPath = "Skin" + Path.DirectorySeparatorChar + skinName;
        _skinDescription = new Document(fullPath + Path.DirectorySeparatorChar + "Description");

        // Read XML data.
        if (_skinDescription.Resource.Element("Skin") != null)
        {
            try
            {
                LoadImagesDescription();
                LoadFontsDescription();
#if (WINDOWS)
                LoadCursorsDescription();
#endif
                LoadControlsDescription();
            }
            catch (Exception e)
            {
                throw new Exception("Failed to load skin: " + skinName + ".\n\n" + e.Message);
            }
        }
        else
        {
            throw new Exception("Failed to load skin: " + skinName + ". Skin tag doesn't exist.");
        }



        try
        {
            foreach (var skinFont in Fonts)
            {
                throw new NotImplementedException();
                //skinFont.Font = (Font)GameManager.ObjectManager.GetObjectByPath(skinFont.FileName);
                //skinFont.Font.LoadTexture("", graphicsDevice);
                //skinFont.Font = new Font();
                //skinFont.Font.LoadTexture(fullPath + Path.DirectorySeparatorChar + "Fonts" + Path.DirectorySeparatorChar + skinFont.FileName, graphicsDevice);
            }
#if (WINDOWS)
            foreach (var skinCursor in Cursors)
            {
                skinCursor.Cursor = new Cursor(game.GraphicsDevice,
                    game.GameManager.AssetContentManager.RootDirectory + Path.DirectorySeparatorChar +
                    fullPath + Path.DirectorySeparatorChar + "Cursors" + Path.DirectorySeparatorChar + skinCursor.Filename + ".cur",
                    game.GameManager.AssetContentManager);
            }
#endif
            foreach (var skinImage in Images)
            {
                var fileName = fullPath + Path.DirectorySeparatorChar + "Textures" + Path.DirectorySeparatorChar + skinImage.Filename + ".png";
                skinImage.Texture = new Texture(game.GraphicsDevice, fileName, game.GameManager.AssetContentManager);
            }
            foreach (var skinControl in Controls)
            {
                foreach (var skinLayer in skinControl.Layers)
                {
                    if (skinLayer.Image.Name != null)
                    {
                        skinLayer.Image = Images[skinLayer.Image.Name];
                    }
                    else
                    {
                        skinLayer.Image = Images[0];
                    }
                    skinLayer.Text.Font = skinLayer.Text.Name != null ? Fonts[skinLayer.Text.Name] : Fonts[0];
                }
            }
        }
        catch (Exception e)
        {
            throw new Exception("Failed to load skin: " + skinName + ".", e);
        }


    } // LoadSkin



    private void LoadControlsDescription()
    {
        if (_skinDescription.Resource.Element("Skin").Element("Controls") == null)
        {
            return;
        }

        foreach (var control in _skinDescription.Resource.Descendants("Control"))
        {
            SkinControlInformation skinControl;
            // Create skin control
            var parent = ReadAttribute(control, "Inherits", null, false);
            var inherit = false;
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
            var name = "";
            ReadAttribute(ref name, inherit, control, "Name", null, true);
            skinControl.Name = name;

            var size = new Size();
            ReadAttribute(ref size.Width, inherit, control.Element("DefaultSize"), "Width", 0, false);
            ReadAttribute(ref size.Height, inherit, control.Element("DefaultSize"), "Height", 0, false);
            skinControl.DefaultSize = size;

            ReadAttribute(ref size.Width, inherit, control.Element("MinimumSize"), "Width", 0, false);
            ReadAttribute(ref size.Height, inherit, control.Element("MinimumSize"), "Height", 0, false);
            skinControl.MinimumSize = size;

            var margin = new Margins();
            ReadAttribute(ref margin.Left, inherit, control.Element("OriginMargins"), "Left", 0, false);
            ReadAttribute(ref margin.Top, inherit, control.Element("OriginMargins"), "Top", 0, false);
            ReadAttribute(ref margin.Right, inherit, control.Element("OriginMargins"), "Right", 0, false);
            ReadAttribute(ref margin.Bottom, inherit, control.Element("OriginMargins"), "Bottom", 0, false);
            skinControl.OriginMargins = margin;

            ReadAttribute(ref margin.Left, inherit, control.Element("ClientMargins"), "Left", 0, false);
            ReadAttribute(ref margin.Top, inherit, control.Element("ClientMargins"), "Top", 0, false);
            ReadAttribute(ref margin.Right, inherit, control.Element("ClientMargins"), "Right", 0, false);
            ReadAttribute(ref margin.Bottom, inherit, control.Element("ClientMargins"), "Bottom", 0, false);
            skinControl.ClientMargins = margin;

            var resizerSize = 0;
            ReadAttribute(ref resizerSize, inherit, control.Element("ResizerSize"), "Value", 0, false);
            skinControl.ResizerSize = resizerSize;

            // Load control's layers
            if (control.Element("Layers") != null)
            {
                foreach (var layer in control.Element("Layers").Elements())
                {
                    if (layer.Name == "Layer")
                    {
                        LoadLayer(skinControl, layer);
                    }
                }
            }
            Controls.Add(skinControl);
        }
    } // LoadControls



    private void LoadLayer(SkinControlInformation skinControl, XElement layerNode)
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

        ReadAttribute(ref margin.Left, inherent, layerNode.Element("SizingMargins"), "Left", 0, false);
        ReadAttribute(ref margin.Top, inherent, layerNode.Element("SizingMargins"), "Top", 0, false);
        ReadAttribute(ref margin.Right, inherent, layerNode.Element("SizingMargins"), "Right", 0, false);
        ReadAttribute(ref margin.Bottom, inherent, layerNode.Element("SizingMargins"), "Bottom", 0, false);
        skinLayer.SizingMargins = margin;

        ReadAttribute(ref margin.Left, inherent, layerNode.Element("ContentMargins"), "Left", 0, false);
        ReadAttribute(ref margin.Top, inherent, layerNode.Element("ContentMargins"), "Top", 0, false);
        ReadAttribute(ref margin.Right, inherent, layerNode.Element("ContentMargins"), "Right", 0, false);
        ReadAttribute(ref margin.Bottom, inherent, layerNode.Element("ContentMargins"), "Bottom", 0, false);
        skinLayer.ContentMargins = margin;


        if (layerNode.Element("States") != null)
        {
            var states = new SkinStates<LayerStates>();

            ReadAttribute(ref integer, inherent, layerNode.Element("States").Element("Enabled"), "Index", 0, false);
            states.Enabled.Index = integer;
            var di = skinLayer.States.Enabled.Index;
            ReadAttribute(ref states.Hovered.Index, inherent, layerNode.Element("States").Element("Hovered"), "Index", di, false);
            states.Hovered.Index = integer;
            ReadAttribute(ref states.Pressed.Index, inherent, layerNode.Element("States").Element("Pressed"), "Index", di, false);
            states.Pressed.Index = integer;
            ReadAttribute(ref states.Focused.Index, inherent, layerNode.Element("States").Element("Focused"), "Index", di, false);
            states.Focused.Index = integer;
            ReadAttribute(ref states.Disabled.Index, inherent, layerNode.Element("States").Element("Disabled"), "Index", di, false);
            states.Disabled.Index = integer;

            ReadAttribute(ref color, inherent, layerNode.Element("States").Element("Enabled"), "Color", Color.White, false);
            states.Enabled.Color = color;
            var dc = skinLayer.States.Enabled.Color;
            ReadAttribute(ref color, inherent, layerNode.Element("States").Element("Hovered"), "Color", dc, false);
            states.Hovered.Color = color;
            ReadAttribute(ref color, inherent, layerNode.Element("States").Element("Pressed"), "Color", dc, false);
            states.Pressed.Color = color;
            ReadAttribute(ref color, inherent, layerNode.Element("States").Element("Focused"), "Color", dc, false);
            states.Focused.Color = color;
            ReadAttribute(ref color, inherent, layerNode.Element("States").Element("Disabled"), "Color", dc, false);
            states.Disabled.Color = color;

            ReadAttribute(ref boolean, inherent, layerNode.Element("States").Element("Enabled"), "Overlay", false, false);
            states.Enabled.Overlay = boolean;
            var dv = skinLayer.States.Enabled.Overlay;
            ReadAttribute(ref boolean, inherent, layerNode.Element("States").Element("Hovered"), "Overlay", dv, false);
            states.Hovered.Overlay = boolean;
            ReadAttribute(ref boolean, inherent, layerNode.Element("States").Element("Pressed"), "Overlay", dv, false);
            states.Pressed.Overlay = boolean;
            ReadAttribute(ref boolean, inherent, layerNode.Element("States").Element("Focused"), "Overlay", dv, false);
            states.Focused.Overlay = boolean;
            ReadAttribute(ref boolean, inherent, layerNode.Element("States").Element("Disabled"), "Overlay", dv, false);
            states.Disabled.Overlay = boolean;

            skinLayer.States = states;
        }



        if (layerNode.Element("Overlays") != null)
        {
            var overlay = new SkinStates<LayerOverlays>();

            ReadAttribute(ref integer, inherent, layerNode.Element("Overlays").Element("Enabled"), "Index", 0, false);
            overlay.Enabled.Index = integer;
            var di = skinLayer.Overlays.Enabled.Index;
            ReadAttribute(ref overlay.Hovered.Index, inherent, layerNode.Element("Overlays").Element("Hovered"), "Index", di, false);
            overlay.Hovered.Index = integer;
            ReadAttribute(ref overlay.Pressed.Index, inherent, layerNode.Element("Overlays").Element("Pressed"), "Index", di, false);
            overlay.Pressed.Index = integer;
            ReadAttribute(ref overlay.Focused.Index, inherent, layerNode.Element("Overlays").Element("Focused"), "Index", di, false);
            overlay.Focused.Index = integer;
            ReadAttribute(ref overlay.Disabled.Index, inherent, layerNode.Element("Overlays").Element("Disabled"), "Index", di, false);
            overlay.Disabled.Index = integer;

            ReadAttribute(ref overlay.Enabled.Color, inherent, layerNode.Element("Overlays").Element("Enabled"), "Color", Color.White, false);
            overlay.Enabled.Color = color;
            var dc = skinLayer.Overlays.Enabled.Color;
            ReadAttribute(ref overlay.Hovered.Color, inherent, layerNode.Element("Overlays").Element("Hovered"), "Color", dc, false);
            overlay.Hovered.Color = color;
            ReadAttribute(ref overlay.Pressed.Color, inherent, layerNode.Element("Overlays").Element("Pressed"), "Color", dc, false);
            overlay.Pressed.Color = color;
            ReadAttribute(ref overlay.Focused.Color, inherent, layerNode.Element("Overlays").Element("Focused"), "Color", dc, false);
            overlay.Focused.Color = color;
            ReadAttribute(ref overlay.Disabled.Color, inherent, layerNode.Element("Overlays").Element("Disabled"), "Color", dc, false);
            overlay.Disabled.Color = color;

            skinLayer.Overlays = overlay;
        }



        if (layerNode.Element("Text") != null)
        {
            var skinText = new SkinText();

            ReadAttribute(ref name, inherent, layerNode.Element("Text"), "Font", null, true);
            skinText.Name = name;
            ReadAttribute(ref integer, inherent, layerNode.Element("Text"), "OffsetX", 0, false);
            skinText.OffsetX = integer;
            ReadAttribute(ref integer, inherent, layerNode.Element("Text"), "OffsetY", 0, false);
            skinText.OffsetY = integer;

            layerAlignment = skinLayer.Text.Alignment.ToString();
            ReadAttribute(ref layerAlignment, inherent, layerNode.Element("Text"), "Alignment", "MiddleCenter", false);
            skinLayer.Text.Alignment = (Alignment)Enum.Parse(typeof(Alignment), layerAlignment, true);

            var colors = new SkinStates<Color>();
            LoadColors(inherent, layerNode.Element("Text"), ref colors);
            skinLayer.Text.Colors = colors;

            skinLayer.Text = skinText;
        }



        if (layerNode.Element("Attributes") != null)
        {
            foreach (var attribute in layerNode.Element("Attributes").Elements())
            {
                if (attribute.Name == "Attribute")
                {
                    LoadLayerAttribute(skinLayer, attribute);
                }
            }
        }


        if (!inherent)
        {
            skinControl.Layers.Add(skinLayer);
        }
    } // LoadLayer


    private void LoadColors(bool inherited, XElement e, ref SkinStates<Color> colors)
    {
        if (e != null)
        {
            ReadAttribute(ref colors.Enabled, inherited, e.Element("Colors").Element("Enabled"), "Color", Color.White, false);
            ReadAttribute(ref colors.Hovered, inherited, e.Element("Colors").Element("Hovered"), "Color", colors.Enabled, false);
            ReadAttribute(ref colors.Pressed, inherited, e.Element("Colors").Element("Pressed"), "Color", colors.Enabled, false);
            ReadAttribute(ref colors.Focused, inherited, e.Element("Colors").Element("Focused"), "Color", colors.Enabled, false);
            ReadAttribute(ref colors.Disabled, inherited, e.Element("Colors").Element("Disabled"), "Color", colors.Enabled, false);
        }
    } // LoadColors



    private void LoadLayerAttribute(SkinLayer skinLayer, XElement e)
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




    private void LoadFontsDescription()
    {
        if (_skinDescription.Resource.Element("Skin").Element("Fonts") == null)
        {
            return;
        }

        foreach (var font in _skinDescription.Resource.Element("Skin").Element("Fonts").Elements())
        {
            var skinFont = new SkinFont
            {
                Name = ReadAttribute(font, "Name", null, true),
                Filename = ReadAttribute(font, "Asset", null, true)
            };
            Fonts.Add(skinFont);
        }
    } // LoadFonts



#if (WINDOWS)
    private void LoadCursorsDescription()
    {
        if (_skinDescription.Resource.Element("Skin").Element("Cursors") == null)
        {
            return;
        }

        foreach (var cursor in _skinDescription.Resource.Element("Skin").Element("Cursors").Elements())
        {
            var skinCursor = new SkinCursor
            {
                Name = ReadAttribute(cursor, "Name", null, true),
                Filename = ReadAttribute(cursor, "Asset", null, true)
            };
            Cursors.Add(skinCursor);
        }
    } // LoadCursors
#endif



    private void LoadImagesDescription()
    {
        if (_skinDescription.Resource.Element("Skin").Element("Images") == null)
        {
            return;
        }

        foreach (var image in _skinDescription.Resource.Element("Skin").Element("Images").Elements())
        {
            var skinImage = new SkinImage
            {
                Name = ReadAttribute(image, "Name", null, true),
                Filename = ReadAttribute(image, "Asset", null, true)
            };
            Images.Add(skinImage);
        }
    } // LoadImages



    private string ReadAttribute(XElement element, string attributeName, string defval, bool needed)
    {
        if (element != null && element.Attribute(attributeName) != null)
        {
            return element.Attribute(attributeName).Value;
        }
        if (needed)
        {
            throw new Exception("Missing required attribute \"" + attributeName + "\" in the skin file.");
        }
        return defval;
    } // ReadAttribute

    private void ReadAttribute(ref string retval, bool inherited, XElement element, string attributeName, string defaultValue, bool needed)
    {
        if (element != null && element.Attribute(attributeName) != null)
        {
            retval = element.Attribute(attributeName).Value;
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

    private void ReadAttribute(ref int retval, bool inherited, XElement element, string attrib, int defval, bool needed)
    {
        var tmp = retval.ToString();
        ReadAttribute(ref tmp, inherited, element, attrib, defval.ToString(), needed);
        retval = int.Parse(tmp);
    } // ReadAttributeInt

    private bool ReadAttribute(XElement element, string attrib, bool defval, bool needed)
    {
        return bool.Parse(ReadAttribute(element, attrib, defval.ToString(), needed));
    } // ReadAttributeBool

    private void ReadAttribute(ref bool retval, bool inherited, XElement element, string attrib, bool defval, bool needed)
    {
        var tmp = retval.ToString();
        ReadAttribute(ref tmp, inherited, element, attrib, defval.ToString(), needed);
        retval = bool.Parse(tmp);
    } // ReadAttributeBool

    private string ColorToString(Color c)
    {
        return string.Format("{0};{1};{2};{3}", c.R, c.G, c.B, c.A);
    } // ColorToString

    private void ReadAttribute(ref Color retval, bool inherited, XElement element, string attrib, Color defval, bool needed)
    {
        var tmp = ColorToString(retval);
        ReadAttribute(ref tmp, inherited, element, attrib, ColorToString(defval), needed);
        retval = Utilities.ParseColor(tmp);
    } // ReadAttributeColor


} // Skin


// XNAFinalEngine.UserInterface
