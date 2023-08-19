using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.FrontEnd.Screen.Gadget;

public abstract class ScreenGadget : ISaveLoad
{
    private Texture2D _whiteTexture;

    public event EventHandler Click;
    public event EventHandler MouseEnter;
    public event EventHandler MouseMove;
    public event EventHandler MouseLeave;
    public event EventHandler SelectedChanged;

    private Renderer2dComponent _renderer2dComponent;
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

    public Renderer2dComponent Renderer2dComponent => _renderer2dComponent;

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

    protected ScreenGadget(JsonElement element, SaveOption opt)
    {
        Load(element, opt);
    }

    public virtual void Initialize(Microsoft.Xna.Framework.Game game)
    {
        _renderer2dComponent = game.GetDrawableGameComponent<Renderer2dComponent>();

        //TODO : faire autrement
        _whiteTexture = new Texture2D(game.GraphicsDevice, 1, 1);
        var whitePixels = new Color[] { Color.White };
        _whiteTexture.SetData<Color>(whitePixels);

        Font = ((CasaEngineGame)game).GameManager.DefaultSpriteFont;
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

    // public it used by ScreenGadgetManipulator
    public abstract void DrawGadget(float elapsedTime);

    public virtual void Load(JsonElement element, SaveOption option)
    {
        var version = element.GetProperty("version").GetInt32();
        AutoSize = element.GetProperty("auto_size").GetBoolean();
        BackgroundColor = element.GetProperty("background_color").GetColor();
        FontName = element.GetProperty("font_name").GetString();
        FontColor = element.GetProperty("font_color").GetColor();
        Width = element.GetProperty("width").GetInt32();
        Height = element.GetProperty("height").GetInt32();
        TabIndex = element.GetProperty("tab_index").GetInt32();
        Location = element.GetProperty("location").GetVector2();
        Scale = element.GetProperty("scale").GetVector2();
        Name = element.GetProperty("name").GetString();
        Text = element.GetProperty("text").GetString();
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

    public static ScreenGadget LoadScreenGadget(JsonElement element, SaveOption option)
    {
        var typeName = element.GetProperty("typeName").GetString();

        switch (typeName)
        {
            case "Label":
                return new ScreenGadgetLabel(element, option);

            case "Button":
                return new ScreenGadgetButton(element, option);

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
    }

    public virtual void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("type", GetType().Name);
        jObject.Add("version", Version);
        jObject.Add("auto_size", AutoSize);
        var newNode = new JObject();
        BackgroundColor.Save(newNode);
        jObject.Add("background_color", newNode);
        jObject.Add("font_name", FontName);
        newNode = new JObject();
        FontColor.Save(newNode);
        jObject.Add("font_color", newNode);
        jObject.Add("width", Width);
        jObject.Add("height", Height);
        jObject.Add("tab_index", TabIndex);
        newNode = new JObject();
        Location.Save(newNode);
        jObject.Add("location", newNode);
        newNode = new JObject();
        Scale.Save(newNode);
        jObject.Add("scale", newNode);
        jObject.Add("name", Name);
        jObject.Add("text", Text);
    }

#endif
}