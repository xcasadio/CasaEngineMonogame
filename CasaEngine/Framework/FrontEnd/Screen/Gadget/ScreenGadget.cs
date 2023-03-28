using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.FrontEnd.Screen.Gadget;

public abstract class ScreenGadget
    : ISaveLoad
{
    private Texture2D _whiteTexture;

    public event EventHandler Click;
    public event EventHandler MouseEnter;
    public event EventHandler MouseMove;
    public event EventHandler MouseLeave;
    public event EventHandler SelectedChanged;

    private Renderer2DComponent _renderer2DComponent;
    private int _width;
    private int _height;
    private Vector2 _location;

    private bool _mouseLeftPressed;
    private bool _mouseOver = false;

    public Rectangle Bounds
    {
        get;
        protected set;
    }

    protected Texture2D WhiteTexture => _whiteTexture;

    public Renderer2DComponent Renderer2DComponent => _renderer2DComponent;

    public bool AutoSize
    {
        get;
        set;
    }

    public bool CanSelect
    {
        get;
        set;
    }

    public int TabIndex
    {
        get;
        set;
    }

    public Color BackgroundColor
    {
        get;
        set;
    }

    public Color FontColor
    {
        get;
        set;
    }

    public SpriteFont Font
    {
        get;
        set;
    }

    public string FontName
    {
        get;
        set;
    }

    public string Text
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    }

    public Vector2 Location
    {
        get => _location;
        set
        {
            _location = value;
            UpdateBounds();
        }
    }

    public int Width
    {
        get => _width;
        set
        {
            _width = value;
            UpdateBounds();
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            _height = value;
            UpdateBounds();
        }
    }

    public Vector2 Scale
    {
        get;
        set;
    }

    protected ScreenGadget(XmlElement el, SaveOption opt)
    {
        Load(el, opt);
    }

    public virtual void Initialize(Microsoft.Xna.Framework.Game game)
    {
        _renderer2DComponent = game.GetDrawableGameComponent<Renderer2DComponent>();

        //TODO : faire autrement
        _whiteTexture = new Texture2D(game.GraphicsDevice, 1, 1);
        var whitePixels = new Color[] { Color.White };
        _whiteTexture.SetData<Color>(whitePixels);

        Font = EngineComponents.DefaultSpriteFont;
        //Font = game_.Content.Load<SpriteFont>(FontName);
    }

    public virtual void Update(float elapsedTime)
    {
        int mouseX = Mouse.GetState().X, mouseY = Mouse.GetState().Y;
        var mouseOver = Bounds.Contains(mouseX, mouseY);

        if (Mouse.GetState().LeftButton == ButtonState.Pressed)
        {
            if (_mouseLeftPressed == false
                && mouseOver)
            {
                if (Click != null)
                {
                    Click.Invoke(this, EventArgs.Empty);
                }
            }

            _mouseLeftPressed = true;
        }
        else
        {
            _mouseLeftPressed = false;
        }

        if (mouseOver
            && _mouseOver == false)
        {
            if (MouseEnter != null)
            {
                MouseEnter.Invoke(this, EventArgs.Empty);
            }
        }
        else if (mouseOver == false
                 && _mouseOver)
        {
            if (MouseLeave != null)
            {
                MouseLeave.Invoke(this, EventArgs.Empty);
            }
        }
        else if (mouseOver)
        {
            mouseOver = true;

            if (MouseMove != null)
            {
                MouseMove.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Draw(float elapsedTime)
    {
        DrawGadget(elapsedTime);
    }

#if EDITOR
    public // used by ScreenGadgetManipulator
#else
        protected
#endif
        abstract void DrawGadget(float elapsedTime);

    public virtual void Load(XmlElement el, SaveOption opt)
    {
        var c = Color.White;
        var v = Vector2.Zero;

        var version = int.Parse(el.Attributes["version"].Value);

        AutoSize = bool.Parse(el.SelectSingleNode("AutoSize").InnerText);
        ((XmlElement)el.SelectSingleNode("BackgroundColor")).Read(ref c);
        BackgroundColor = c;
        FontName = el.SelectSingleNode("FontName").InnerText;
        ((XmlElement)el.SelectSingleNode("FontColor")).Read(ref c);
        FontColor = c;
        Width = int.Parse(el.SelectSingleNode("Width").InnerText);
        Height = int.Parse(el.SelectSingleNode("Height").InnerText);
        TabIndex = int.Parse(el.SelectSingleNode("TabIndex").InnerText);
        ((XmlElement)el.SelectSingleNode("Location")).Read(ref v);
        Location = v;
        ((XmlElement)el.SelectSingleNode("Scale")).Read(ref v);
        Scale = v;
        Name = el.SelectSingleNode("Name").InnerText;
        Text = el.SelectSingleNode("Text").InnerText;
    }

    public virtual void Load(BinaryReader br, SaveOption opt)
    {
        throw new NotImplementedException();
    }

    private void UpdateBounds()
    {
        Bounds = new Rectangle((int)Location.X, (int)Location.Y, _width, _height);
    }

    public bool Compare(ScreenGadget g)
    {
        return _width == g._width
               && _height == g._height
               && _location == g._location
               && AutoSize == g.AutoSize
               && CanSelect == g.CanSelect
               && TabIndex == g.TabIndex
               && BackgroundColor == g.BackgroundColor
               && FontColor == g.FontColor
               && Font == g.Font
               && Text == g.Text
               && Name == g.Name
               && Scale == g.Scale;
    }

    public static ScreenGadget LoadScreenGadget(XmlElement el, SaveOption opt)
    {
        var typeName = el.Attributes["typeName"].Value;

        switch (typeName)
        {
            case "Label":
                return new ScreenGadgetLabel(el, opt);

            case "Button":
                return new ScreenGadgetButton(el, opt);

            default:
                throw new InvalidOperationException("ScreenGadget.LoadScreen() : the screen type " + typeName + " is not supported.");
        }
    }

#if EDITOR
    private static readonly int Version = 1;

    protected ScreenGadget(string name)
    {
        Scale = Vector2.One;
        Name = name;
        Text = Name;
        Font = EngineComponents.DefaultSpriteFont;
    }

    public virtual void Save(XmlElement el, SaveOption opt)
    {
        XmlElement node;

        el.OwnerDocument.AddAttribute(el, "type", GetType().Name);
        el.OwnerDocument.AddAttribute(el, "version", Version.ToString());

        node = el.OwnerDocument.CreateElementWithText("AutoSize", AutoSize.ToString());
        el.AppendChild(node);
        node = el.OwnerDocument.CreateElement("BackgroundColor", BackgroundColor);
        el.AppendChild(node);
        node = el.OwnerDocument.CreateElement("FontName", FontName);
        el.AppendChild(node);
        node = el.OwnerDocument.CreateElement("FontColor", FontColor);
        el.AppendChild(node);
        node = el.OwnerDocument.CreateElement("Width", Width.ToString());
        el.AppendChild(node);
        node = el.OwnerDocument.CreateElement("Height", Height.ToString());
        el.AppendChild(node);
        node = el.OwnerDocument.CreateElement("TabIndex", TabIndex.ToString());
        el.AppendChild(node);
        node = el.OwnerDocument.CreateElement("Location", Location);
        el.AppendChild(node);
        node = el.OwnerDocument.CreateElement("Scale", Scale);
        el.AppendChild(node);
        node = el.OwnerDocument.CreateElement("Name", Name);
        el.AppendChild(node);
        node = el.OwnerDocument.CreateElement("Text", Text);
        el.AppendChild(node);
    }

    public virtual void Save(BinaryWriter bw, SaveOption opt)
    {
        bw.Write(GetType().Name);
        bw.Write(Version);
        bw.Write(AutoSize);
        bw.Write(BackgroundColor);
        bw.Write(FontName);
        bw.Write(FontColor);
        bw.Write(Width);
        bw.Write(Height);
        bw.Write(TabIndex);
        bw.Write(Location);
        bw.Write(Scale);
        bw.Write(Name);
        bw.Write(Text);
    }
#endif
}