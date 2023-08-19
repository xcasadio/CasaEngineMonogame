using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.UserInterface;
using Newtonsoft.Json.Linq;
using Control = CasaEngine.Framework.UserInterface.Control;

namespace CasaEngine.Framework.FrontEnd.Screen;

public class UiScreen : Screen
{
    private readonly List<Control> _controls = new();

    public Control[] Gagdets => _controls.ToArray();

    public UiScreen(string name)
        : base(name)
    {
        TransitionOnTime = TimeSpan.FromSeconds(0.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
    }

    public UiScreen(JsonElement element, SaveOption option)
        : base(element, option)
    {
        TransitionOnTime = TimeSpan.FromSeconds(0.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
    }

    public override void LoadContent(Microsoft.Xna.Framework.Game r)
    {
        base.LoadContent(r);

        /*foreach (Control g in _Controls)
        {
            g.Initialize(r_.Game);
        }*/
    }

    public override void Draw(float elapsedTime)
    {
        /*foreach (Control g in _Controls)
        {
            g.Draw(elapsedTime_);
        }*/
    }

    public override void HandleInput(InputState input)
    {

    }

    public override void Update(float elapsedTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(elapsedTime, otherScreenHasFocus, coveredByOtherScreen);

        foreach (var g in _controls)
        {
            g.Update(elapsedTime);
        }
    }

    public Control GetGadget(string name)
    {
        foreach (var g in _controls)
        {
            if (g.Name.Equals(name))
            {
                return g;
            }
        }

        return null;
    }

    public override void CopyFrom(Screen screen)
    {
        base.CopyFrom(screen);

        if (screen is UiScreen)
        {
            var e = screen as UiScreen;
            _controls.Clear();
            _controls.AddRange(e._controls);
        }
        else
        {
            throw new InvalidCastException("EditorScreen.CopyFrom() : Screen is not a EditorScreen");
        }
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);

        foreach (var gadgetElement in element.GetProperty("gadgets").EnumerateArray())
        {
            _controls.Add(FactoryUiControl.LoadControl(gadgetElement, option));
        }
    }

#if EDITOR

    public bool CompareTo(object other)
    {
        if (base.CompareTo(other) == false)
        {
            return false;
        }

        var res = true;

        if (other is UiScreen)
        {
            var e = other as UiScreen;

            if (_controls.Count != e._controls.Count)
            {
                res = false;
            }
            else
            {
                for (var i = 0; i < _controls.Count; i++)
                {
                    //res &= e._Controls[i].Compare(_Controls[i]);
                }
            }
        }
        else
        {
            throw new InvalidCastException("EditorScreen.Compare() : Screen is not a EditorScreen");
        }

        return res;
    }

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
    }

#endif
}