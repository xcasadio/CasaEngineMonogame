using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using FontStashSharp;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace TomShane.Neoforce.Controls;

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
    }
}

public struct LayerStates
{
    public int Index;
    public Color Color;
    public bool Overlay;
}

public struct LayerOverlays
{
    public int Index;
    public Color Color;
}

public struct SkinInfo
{
    public string Name;
    public string Description;
    public string Author;
    public string Version;
}

public class SkinList<T> : List<T>
{

    public T this[string index]
    {
        get
        {
            for (var i = 0; i < Count; i++)
            {
                var s = (SkinBase)(object)this[i];
                if (s.Name.ToLower() == index.ToLower())
                {
                    return this[i];
                }
            }
            return default(T);
        }

        set
        {
            for (var i = 0; i < Count; i++)
            {
                var s = (SkinBase)(object)this[i];
                if (s.Name.ToLower() == index.ToLower())
                {
                    this[i] = value;
                }
            }
        }
    }

    public SkinList()
        : base()
    {
    }

    public SkinList(SkinList<T> source)
        : base()
    {
        for (var i = 0; i < source.Count; i++)
        {
            var t = new Type[1];
            t[0] = typeof(T);

            var p = new object[1];
            p[0] = source[i];

            Add((T)t[0].GetConstructor(t).Invoke(p));
        }
    }

}

public class SkinBase
{

    public string Name;
    public bool Archive;

    public SkinBase()
        : base()
    {
        Archive = false;
    }

    public SkinBase(SkinBase source)
        : base()
    {
        if (source != null)
        {
            Name = source.Name;
            Archive = source.Archive;
        }
    }

}

public class SkinLayer : SkinBase
{

    public SkinImage Image = new();
    public int Width;
    public int Height;
    public int OffsetX;
    public int OffsetY;
    public Alignment Alignment;
    public Margins SizingMargins;
    public Margins ContentMargins;
    public SkinStates<LayerStates> States;
    public SkinStates<LayerOverlays> Overlays;
    public readonly SkinText Text = new();
    public readonly SkinList<SkinAttribute> Attributes = new();

    public SkinLayer()
        : base()
    {
        States.Enabled.Color = Color.White;
        States.Pressed.Color = Color.White;
        States.Focused.Color = Color.White;
        States.Hovered.Color = Color.White;
        States.Disabled.Color = Color.White;

        Overlays.Enabled.Color = Color.White;
        Overlays.Pressed.Color = Color.White;
        Overlays.Focused.Color = Color.White;
        Overlays.Hovered.Color = Color.White;
        Overlays.Disabled.Color = Color.White;
    }

    public SkinLayer(SkinLayer source)
        : base(source)
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
    }

}

public class SkinText : SkinBase
{

    public SkinFont Font;
    public int OffsetX;
    public int OffsetY;
    public Alignment Alignment;
    public SkinStates<Color> Colors;

    public SkinText()
        : base()
    {
        Colors.Enabled = Color.White;
        Colors.Pressed = Color.White;
        Colors.Focused = Color.White;
        Colors.Hovered = Color.White;
        Colors.Disabled = Color.White;
    }

    public SkinText(SkinText source)
        : base(source)
    {
        if (source != null)
        {
            Font = new SkinFont(source.Font);
            OffsetX = source.OffsetX;
            OffsetY = source.OffsetY;
            Alignment = source.Alignment;
            Colors = source.Colors;
        }
    }

}

public class SkinFont : SkinBase
{

    public SpriteFontBase Resource;
    public string Asset;
    public string Addon;

    public int Height
    {
        get
        {
            if (Resource != null)
            {
                return (int)Resource.MeasureString("AaYy").Y;
            }
            return 0;
        }
    }

    public SkinFont()
        : base()
    {
    }

    public SkinFont(SkinFont source)
        : base(source)
    {
        if (source != null)
        {
            Resource = source.Resource;
            Asset = source.Asset;
        }
    }

}

public class SkinImage : SkinBase
{

    public Texture2D Resource;
    public string Asset;
    public string Addon;

    public SkinImage()
        : base()
    {
    }

    public SkinImage(SkinImage source)
        : base(source)
    {
        Resource = source.Resource;
        Asset = source.Asset;
    }

}

public class SkinCursor : SkinBase
{
    public Cursor Resource;

    public string Asset;
    public string Addon;

    public SkinCursor()
        : base()
    {
    }

    public SkinCursor(SkinCursor source)
        : base(source)
    {
        Resource = source.Resource;

        Asset = source.Asset;
    }

}

public class SkinControl : SkinBase
{

    public string Inherits;
    public Size DefaultSize;
    public int ResizerSize;
    public Size MinimumSize;
    public Margins OriginMargins;
    public Margins ClientMargins;
    public readonly SkinList<SkinLayer> Layers = new();
    public readonly SkinList<SkinAttribute> Attributes = new();

    public SkinControl()
        : base()
    {
    }

    public SkinControl(SkinControl source)
        : base(source)
    {
        Inherits = source.Inherits;
        DefaultSize = source.DefaultSize;
        MinimumSize = source.MinimumSize;
        OriginMargins = source.OriginMargins;
        ClientMargins = source.ClientMargins;
        ResizerSize = source.ResizerSize;
        Layers = new SkinList<SkinLayer>(source.Layers);
        Attributes = new SkinList<SkinAttribute>(source.Attributes);
    }

}

public class SkinAttribute : SkinBase
{

    public string Value;

    public SkinAttribute()
        : base()
    {
    }

    public SkinAttribute(SkinAttribute source)
        : base(source)
    {
        Value = source.Value;
    }

}

public class Skin : Component
{

    SkinXmlDocument _doc;
    private string _name;
    private Version _version;
    private SkinInfo _info;
    private SkinList<SkinControl> _controls;
    private SkinList<SkinFont> _fonts;
    private SkinList<SkinCursor> _cursors;
    private SkinList<SkinImage> _images;
    private SkinList<SkinAttribute> _attributes;
    private ArchiveManager _content;

    public virtual string Name => _name;
    public virtual Version Version => _version;
    public virtual SkinInfo Info => _info;
    public virtual SkinList<SkinControl> Controls => _controls;
    public virtual SkinList<SkinFont> Fonts => _fonts;
    public virtual SkinList<SkinCursor> Cursors => _cursors;
    public virtual SkinList<SkinImage> Images => _images;
    public virtual SkinList<SkinAttribute> Attributes => _attributes;

    public Skin(Manager manager, string name)
        : base(manager)
    {
        _name = name;
        _content = new ArchiveManager(Manager.Game.Services, GetArchiveLocation(name + Manager.SkinExtension));
        _content.RootDirectory = GetFolder();
        _doc = new SkinXmlDocument();
        _controls = new SkinList<SkinControl>();
        _fonts = new SkinList<SkinFont>();
        _images = new SkinList<SkinImage>();
        _cursors = new SkinList<SkinCursor>();
        _attributes = new SkinList<SkinAttribute>();

        LoadSkin(null, _content.UseArchive);

        var folder = GetAddonsFolder();
        if (folder == "")
        {
            _content.UseArchive = true;
            folder = "Addons\\";
        }
        else
        {
            _content.UseArchive = false;
        }

        var addons = _content.GetDirectories(folder);

        if (addons != null && addons.Length > 0)
        {
            for (var i = 0; i < addons.Length; i++)
            {
                var d = new DirectoryInfo(GetAddonsFolder() + addons[i]);
                if (!((d.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) || _content.UseArchive)
                {
                    LoadSkin(addons[i].Replace("\\", ""), _content.UseArchive);
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_content != null)
            {
                _content.Unload();
                _content.Dispose();
                _content = null;
            }
        }

        base.Dispose(disposing);
    }

    private string GetArchiveLocation(string name)
    {
        var path = Path.GetFullPath(Manager.SkinDirectory) + Path.GetFileNameWithoutExtension(name) + "\\";
        if (!Directory.Exists(path) || !File.Exists(path + "Skin.xnb"))
        {
            path = Path.GetFullPath(Manager.SkinDirectory) + name;
            return path;
        }

        return null;
    }

    private string GetFolder()
    {
        var path = Path.GetFullPath(Manager.SkinDirectory) + _name + "\\";
        if (!Directory.Exists(path) || !File.Exists(path + "Skin.xnb"))
        {
            path = "";
        }

        return path;
    }

    private string GetAddonsFolder()
    {
        var path = Path.GetFullPath(Manager.SkinDirectory) + _name + "\\Addons\\";
        if (!Directory.Exists(path))
        {
            path = Path.GetFullPath(".\\Content\\Skins\\") + _name + "\\Addons\\";
            if (!Directory.Exists(path))
            {
                path = Path.GetFullPath(".\\Skins\\") + _name + "\\Addons\\";
            }
        }

        return path;
    }

    private string GetFolder(string type)
    {
        return GetFolder() + type + "\\";
    }

    private string GetAsset(string type, string asset, string addon)
    {
        var ret = GetFolder(type) + asset;
        if (addon != null && addon != "")
        {
            ret = GetAddonsFolder() + addon + "\\" + type + "\\" + asset;
        }
        return ret;
    }

    public override void Init()
    {
        base.Init();

        for (var i = 0; i < _fonts.Count; i++)
        {
            _content.UseArchive = _fonts[i].Archive;
            var asset = GetAsset("Fonts", _fonts[i].Asset, _fonts[i].Addon);
            asset = _content.UseArchive ? asset : Path.GetFullPath(asset);

            //TODO
            var fontSystem = new FontSystem();
            fontSystem.AddFont(File.ReadAllBytes(@"C:\\Windows\\Fonts\\Tahoma.ttf"));
            SpriteFontBase font8 = fontSystem.GetFont(12);
            _fonts[i].Resource = font8;
            //(fonts[i].Resource) = content.Load<SpriteFont>(asset);
        }

        for (var i = 0; i < _cursors.Count; i++)
        {
            _content.UseArchive = _cursors[i].Archive;
            var asset = GetAsset("Cursors", _cursors[i].Asset, _cursors[i].Addon);
            asset = _content.UseArchive ? asset : Path.GetFullPath(asset);
            _cursors[i].Resource = _content.Load<Cursor>(asset);
        }

        for (var i = 0; i < _images.Count; i++)
        {
            _content.UseArchive = _images[i].Archive;
            var asset = GetAsset("Images", _images[i].Asset, _images[i].Addon);
            asset = _content.UseArchive ? asset : Path.GetFullPath(asset);
            _images[i].Resource = _content.Load<Texture2D>(asset);
        }

        for (var i = 0; i < _controls.Count; i++)
        {
            for (var j = 0; j < _controls[i].Layers.Count; j++)
            {
                if (_controls[i].Layers[j].Image.Name != null)
                {
                    _controls[i].Layers[j].Image = _images[_controls[i].Layers[j].Image.Name];
                }
                else
                {
                    _controls[i].Layers[j].Image = _images[0];
                }

                if (_controls[i].Layers[j].Text.Name != null)
                {
                    _controls[i].Layers[j].Text.Font = _fonts[_controls[i].Layers[j].Text.Name];
                }
                else
                {
                    _controls[i].Layers[j].Text.Font = _fonts[0];
                }
            }
        }
    }

    private string ReadAttribute(XmlElement element, string attrib, string defval, bool needed)
    {
        if (element != null && element.HasAttribute(attrib))
        {
            return element.Attributes[attrib].Value;
        }

        if (needed)
        {
            throw new Exception("Missing required attribute \"" + attrib + "\" in the skin file.");
        }
        return defval;
    }

    private void ReadAttribute(ref string retval, bool inherited, XmlElement element, string attrib, string defval, bool needed)
    {
        if (element != null && element.HasAttribute(attrib))
        {
            retval = element.Attributes[attrib].Value;
        }
        else if (inherited)
        {
        }
        else if (needed)
        {
            throw new Exception("Missing required attribute \"" + attrib + "\" in the skin file.");
        }
        else
        {
            retval = defval;
        }
    }

    private int ReadAttributeInt(XmlElement element, string attrib, int defval, bool needed)
    {
        return int.Parse(ReadAttribute(element, attrib, defval.ToString(), needed));
    }

    private void ReadAttributeInt(ref int retval, bool inherited, XmlElement element, string attrib, int defval, bool needed)
    {
        var tmp = retval.ToString();
        ReadAttribute(ref tmp, inherited, element, attrib, defval.ToString(), needed);
        retval = int.Parse(tmp);
    }

    private bool ReadAttributeBool(XmlElement element, string attrib, bool defval, bool needed)
    {
        return bool.Parse(ReadAttribute(element, attrib, defval.ToString(), needed));
    }

    private void ReadAttributeBool(ref bool retval, bool inherited, XmlElement element, string attrib, bool defval, bool needed)
    {
        var tmp = retval.ToString();
        ReadAttribute(ref tmp, inherited, element, attrib, defval.ToString(), needed);
        retval = bool.Parse(tmp);
    }

    private byte ReadAttributeByte(XmlElement element, string attrib, byte defval, bool needed)
    {
        return byte.Parse(ReadAttribute(element, attrib, defval.ToString(), needed));
    }

    private void ReadAttributeByte(ref byte retval, bool inherited, XmlElement element, string attrib, byte defval, bool needed)
    {
        var tmp = retval.ToString();
        ReadAttribute(ref tmp, inherited, element, attrib, defval.ToString(), needed);
        retval = byte.Parse(tmp);
    }

    private string ColorToString(Color c)
    {
        return $"{c.R};{c.G};{c.B};{c.A}";
    }

    private void ReadAttributeColor(ref Color retval, bool inherited, XmlElement element, string attrib, Color defval, bool needed)
    {
        var tmp = ColorToString(retval);
        ReadAttribute(ref tmp, inherited, element, attrib, ColorToString(defval), needed);
        retval = Utilities.ParseColor(tmp);
    }

    private void LoadSkin(string addon, bool archive)
    {
        try
        {
            var isaddon = addon != null && addon != "";
            var file = GetFolder();
            if (isaddon)
            {
                file = GetAddonsFolder() + addon + "\\";
            }
            file += "Skin";

            file = archive ? file : Path.GetFullPath(file);
            _doc = _content.Load<SkinXmlDocument>(file);

            var e = _doc["Skin"];
            if (e != null)
            {
                var xname = ReadAttribute(e, "Name", null, true);
                if (!isaddon)
                {
                    if (_name.ToLower() != xname.ToLower())
                    {
                        throw new Exception("Skin name defined in the skin file doesn't match requested skin.");
                    }

                    _name = xname;
                }
                else
                {
                    if (addon.ToLower() != xname.ToLower())
                    {
                        throw new Exception("Skin name defined in the skin file doesn't match addon name.");
                    }
                }

                Version xversion = null;
                try
                {
                    xversion = new Version(ReadAttribute(e, "Version", "0.0.0.0", false));
                }
                catch (Exception x)
                {
                    throw new Exception("Unable to resolve skin file version. " + x.Message);
                }

                if (xversion != Manager.SkinVersion)
                {
                    throw new Exception("This version of Neoforce Controls can only read skin files in version of " + Manager.SkinVersion.ToString() + ".");
                }

                if (!isaddon)
                {
                    _version = xversion;
                }

                if (!isaddon)
                {
                    var ei = e["Info"];
                    if (ei != null)
                    {
                        if (ei["Name"] != null)
                        {
                            _info.Name = ei["Name"].InnerText;
                        }

                        if (ei["Description"] != null)
                        {
                            _info.Description = ei["Description"].InnerText;
                        }

                        if (ei["Author"] != null)
                        {
                            _info.Author = ei["Author"].InnerText;
                        }

                        if (ei["Version"] != null)
                        {
                            _info.Version = ei["Version"].InnerText;
                        }
                    }
                }

                LoadImages(addon, archive);
                LoadFonts(addon, archive);
                LoadCursors(addon, archive);
                LoadSkinAttributes();
                LoadControls();
            }
        }
        catch (Exception x)
        {
            throw new Exception("Unable to load skin file. " + x.Message);
        }
    }

    private void LoadSkinAttributes()
    {
        var l = _doc["Skin"]["Attributes"]?.GetElementsByTagName("Attribute");

        if (l != null && l.Count > 0)
        {
            foreach (XmlElement e in l)
            {
                var sa = new SkinAttribute();
                sa.Name = ReadAttribute(e, "Name", null, true);
                sa.Value = ReadAttribute(e, "Value", null, true);
                _attributes.Add(sa);
            }
        }
    }

    private void LoadControls()
    {
        var l = _doc["Skin"]["Controls"]?.GetElementsByTagName("Control");

        if (l != null && l.Count > 0)
        {
            foreach (XmlElement e in l)
            {
                SkinControl sc = null;
                var parent = ReadAttribute(e, "Inherits", null, false);
                var inh = false;

                if (parent != null)
                {
                    sc = new SkinControl(_controls[parent]);
                    sc.Inherits = parent;
                    inh = true;
                }
                else
                {
                    sc = new SkinControl();
                }

                ReadAttribute(ref sc.Name, inh, e, "Name", null, true);

                ReadAttributeInt(ref sc.DefaultSize.Width, inh, e["DefaultSize"], "Width", 0, false);
                ReadAttributeInt(ref sc.DefaultSize.Height, inh, e["DefaultSize"], "Height", 0, false);

                ReadAttributeInt(ref sc.MinimumSize.Width, inh, e["MinimumSize"], "Width", 0, false);
                ReadAttributeInt(ref sc.MinimumSize.Height, inh, e["MinimumSize"], "Height", 0, false);

                ReadAttributeInt(ref sc.OriginMargins.Left, inh, e["OriginMargins"], "Left", 0, false);
                ReadAttributeInt(ref sc.OriginMargins.Top, inh, e["OriginMargins"], "Top", 0, false);
                ReadAttributeInt(ref sc.OriginMargins.Right, inh, e["OriginMargins"], "Right", 0, false);
                ReadAttributeInt(ref sc.OriginMargins.Bottom, inh, e["OriginMargins"], "Bottom", 0, false);

                ReadAttributeInt(ref sc.ClientMargins.Left, inh, e["ClientMargins"], "Left", 0, false);
                ReadAttributeInt(ref sc.ClientMargins.Top, inh, e["ClientMargins"], "Top", 0, false);
                ReadAttributeInt(ref sc.ClientMargins.Right, inh, e["ClientMargins"], "Right", 0, false);
                ReadAttributeInt(ref sc.ClientMargins.Bottom, inh, e["ClientMargins"], "Bottom", 0, false);

                ReadAttributeInt(ref sc.ResizerSize, inh, e["ResizerSize"], "Value", 0, false);

                var l2 = e["Layers"]?.GetElementsByTagName("Layer");
                if (l2 != null && l2.Count > 0)
                {
                    LoadLayers(sc, l2);
                }

                var l3 = e["Attributes"]?.GetElementsByTagName("Attribute");
                if (l3 != null && l3.Count > 0)
                {
                    LoadControlAttributes(sc, l3);
                }
                _controls.Add(sc);
            }
        }
    }

    private void LoadFonts(string addon, bool archive)
    {
        var l = _doc["Skin"]["Fonts"]?.GetElementsByTagName("Font");
        if (l != null && l.Count > 0)
        {
            foreach (XmlElement e in l)
            {
                var sf = new SkinFont();
                sf.Name = ReadAttribute(e, "Name", null, true);
                sf.Archive = archive;
                sf.Asset = ReadAttribute(e, "Asset", null, true);
                sf.Addon = addon;
                _fonts.Add(sf);
            }
        }
    }

    private void LoadCursors(string addon, bool archive)
    {
        var l = _doc["Skin"]["Cursors"]?.GetElementsByTagName("Cursor");
        if (l != null && l.Count > 0)
        {
            foreach (XmlElement e in l)
            {
                var sc = new SkinCursor();
                sc.Name = ReadAttribute(e, "Name", null, true);
                sc.Archive = archive;
                sc.Asset = ReadAttribute(e, "Asset", null, true);
                sc.Addon = addon;
                _cursors.Add(sc);
            }
        }
    }

    private void LoadImages(string addon, bool archive)
    {
        var l = _doc["Skin"]["Images"]?.GetElementsByTagName("Image");
        if (l != null && l.Count > 0)
        {
            foreach (XmlElement e in l)
            {
                var si = new SkinImage();
                si.Name = ReadAttribute(e, "Name", null, true);
                si.Archive = archive;
                si.Asset = ReadAttribute(e, "Asset", null, true);
                si.Addon = addon;
                _images.Add(si);
            }
        }
    }

    private void LoadLayers(SkinControl sc, XmlNodeList l)
    {
        foreach (XmlElement e in l)
        {
            var name = ReadAttribute(e, "Name", null, true);
            var over = ReadAttributeBool(e, "Override", false, false);
            var sl = sc.Layers[name];
            var inh = true;

            if (sl == null)
            {
                sl = new SkinLayer();
                inh = false;
            }

            if (inh && over)
            {
                sl = new SkinLayer();
                sc.Layers[name] = sl;
            }

            ReadAttribute(ref sl.Name, inh, e, "Name", null, true);
            ReadAttribute(ref sl.Image.Name, inh, e, "Image", "Control", false);
            ReadAttributeInt(ref sl.Width, inh, e, "Width", 0, false);
            ReadAttributeInt(ref sl.Height, inh, e, "Height", 0, false);

            var tmp = sl.Alignment.ToString();
            ReadAttribute(ref tmp, inh, e, "Alignment", "MiddleCenter", false);
            sl.Alignment = (Alignment)Enum.Parse(typeof(Alignment), tmp, true);

            ReadAttributeInt(ref sl.OffsetX, inh, e, "OffsetX", 0, false);
            ReadAttributeInt(ref sl.OffsetY, inh, e, "OffsetY", 0, false);

            ReadAttributeInt(ref sl.SizingMargins.Left, inh, e["SizingMargins"], "Left", 0, false);
            ReadAttributeInt(ref sl.SizingMargins.Top, inh, e["SizingMargins"], "Top", 0, false);
            ReadAttributeInt(ref sl.SizingMargins.Right, inh, e["SizingMargins"], "Right", 0, false);
            ReadAttributeInt(ref sl.SizingMargins.Bottom, inh, e["SizingMargins"], "Bottom", 0, false);

            ReadAttributeInt(ref sl.ContentMargins.Left, inh, e["ContentMargins"], "Left", 0, false);
            ReadAttributeInt(ref sl.ContentMargins.Top, inh, e["ContentMargins"], "Top", 0, false);
            ReadAttributeInt(ref sl.ContentMargins.Right, inh, e["ContentMargins"], "Right", 0, false);
            ReadAttributeInt(ref sl.ContentMargins.Bottom, inh, e["ContentMargins"], "Bottom", 0, false);

            if (e["States"] != null)
            {
                ReadAttributeInt(ref sl.States.Enabled.Index, inh, e["States"]["Enabled"], "Index", 0, false);
                var di = sl.States.Enabled.Index;
                ReadAttributeInt(ref sl.States.Hovered.Index, inh, e["States"]["Hovered"], "Index", di, false);
                ReadAttributeInt(ref sl.States.Pressed.Index, inh, e["States"]["Pressed"], "Index", di, false);
                ReadAttributeInt(ref sl.States.Focused.Index, inh, e["States"]["Focused"], "Index", di, false);
                ReadAttributeInt(ref sl.States.Disabled.Index, inh, e["States"]["Disabled"], "Index", di, false);

                ReadAttributeColor(ref sl.States.Enabled.Color, inh, e["States"]["Enabled"], "Color", Color.White, false);
                var dc = sl.States.Enabled.Color;
                ReadAttributeColor(ref sl.States.Hovered.Color, inh, e["States"]["Hovered"], "Color", dc, false);
                ReadAttributeColor(ref sl.States.Pressed.Color, inh, e["States"]["Pressed"], "Color", dc, false);
                ReadAttributeColor(ref sl.States.Focused.Color, inh, e["States"]["Focused"], "Color", dc, false);
                ReadAttributeColor(ref sl.States.Disabled.Color, inh, e["States"]["Disabled"], "Color", dc, false);

                ReadAttributeBool(ref sl.States.Enabled.Overlay, inh, e["States"]["Enabled"], "Overlay", false, false);
                var dv = sl.States.Enabled.Overlay;
                ReadAttributeBool(ref sl.States.Hovered.Overlay, inh, e["States"]["Hovered"], "Overlay", dv, false);
                ReadAttributeBool(ref sl.States.Pressed.Overlay, inh, e["States"]["Pressed"], "Overlay", dv, false);
                ReadAttributeBool(ref sl.States.Focused.Overlay, inh, e["States"]["Focused"], "Overlay", dv, false);
                ReadAttributeBool(ref sl.States.Disabled.Overlay, inh, e["States"]["Disabled"], "Overlay", dv, false);
            }

            if (e["Overlays"] != null)
            {
                ReadAttributeInt(ref sl.Overlays.Enabled.Index, inh, e["Overlays"]["Enabled"], "Index", 0, false);
                var di = sl.Overlays.Enabled.Index;
                ReadAttributeInt(ref sl.Overlays.Hovered.Index, inh, e["Overlays"]["Hovered"], "Index", di, false);
                ReadAttributeInt(ref sl.Overlays.Pressed.Index, inh, e["Overlays"]["Pressed"], "Index", di, false);
                ReadAttributeInt(ref sl.Overlays.Focused.Index, inh, e["Overlays"]["Focused"], "Index", di, false);
                ReadAttributeInt(ref sl.Overlays.Disabled.Index, inh, e["Overlays"]["Disabled"], "Index", di, false);

                ReadAttributeColor(ref sl.Overlays.Enabled.Color, inh, e["Overlays"]["Enabled"], "Color", Color.White, false);
                var dc = sl.Overlays.Enabled.Color;
                ReadAttributeColor(ref sl.Overlays.Hovered.Color, inh, e["Overlays"]["Hovered"], "Color", dc, false);
                ReadAttributeColor(ref sl.Overlays.Pressed.Color, inh, e["Overlays"]["Pressed"], "Color", dc, false);
                ReadAttributeColor(ref sl.Overlays.Focused.Color, inh, e["Overlays"]["Focused"], "Color", dc, false);
                ReadAttributeColor(ref sl.Overlays.Disabled.Color, inh, e["Overlays"]["Disabled"], "Color", dc, false);
            }

            if (e["Text"] != null)
            {
                ReadAttribute(ref sl.Text.Name, inh, e["Text"], "Font", null, true);
                ReadAttributeInt(ref sl.Text.OffsetX, inh, e["Text"], "OffsetX", 0, false);
                ReadAttributeInt(ref sl.Text.OffsetY, inh, e["Text"], "OffsetY", 0, false);

                tmp = sl.Text.Alignment.ToString();
                ReadAttribute(ref tmp, inh, e["Text"], "Alignment", "MiddleCenter", false);
                sl.Text.Alignment = (Alignment)Enum.Parse(typeof(Alignment), tmp, true);

                LoadColors(inh, e["Text"], ref sl.Text.Colors);
            }

            var l2 = e["Attributes"]?.GetElementsByTagName("Attribute");
            if (l2 != null && l2.Count > 0)
            {
                LoadLayerAttributes(sl, l2);
            }
            if (!inh)
            {
                sc.Layers.Add(sl);
            }
        }
    }

    private void LoadColors(bool inherited, XmlElement e, ref SkinStates<Color> colors)
    {
        if (e != null)
        {
            ReadAttributeColor(ref colors.Enabled, inherited, e["Colors"]["Enabled"], "Color", Color.White, false);
            ReadAttributeColor(ref colors.Hovered, inherited, e["Colors"]["Hovered"], "Color", colors.Enabled, false);
            ReadAttributeColor(ref colors.Pressed, inherited, e["Colors"]["Pressed"], "Color", colors.Enabled, false);
            ReadAttributeColor(ref colors.Focused, inherited, e["Colors"]["Focused"], "Color", colors.Enabled, false);
            ReadAttributeColor(ref colors.Disabled, inherited, e["Colors"]["Disabled"], "Color", colors.Enabled, false);
        }
    }

    private void LoadControlAttributes(SkinControl sc, XmlNodeList l)
    {
        foreach (XmlElement e in l)
        {
            var name = ReadAttribute(e, "Name", null, true);
            var sa = sc.Attributes[name];
            var inh = true;

            if (sa == null)
            {
                sa = new SkinAttribute();
                inh = false;
            }

            sa.Name = name;
            ReadAttribute(ref sa.Value, inh, e, "Value", null, true);

            if (!inh)
            {
                sc.Attributes.Add(sa);
            }
        }
    }

    private void LoadLayerAttributes(SkinLayer sl, XmlNodeList l)
    {
        foreach (XmlElement e in l)
        {
            var name = ReadAttribute(e, "Name", null, true);
            var sa = sl.Attributes[name];
            var inh = true;

            if (sa == null)
            {
                sa = new SkinAttribute();
                inh = false;
            }

            sa.Name = name;
            ReadAttribute(ref sa.Value, inh, e, "Value", null, true);

            if (!inh)
            {
                sl.Attributes.Add(sa);
            }
        }
    }

}