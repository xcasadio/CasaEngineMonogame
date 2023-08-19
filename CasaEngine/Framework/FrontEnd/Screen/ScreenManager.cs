using System.Text.Json;
using CasaEngine.Core.Design;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.FrontEnd.Screen;

public class ScreenManager
{
    private readonly List<UiScreen> _screens = new();

    public void Load(JsonElement element, SaveOption opt)
    {
        var version = element.GetProperty("version").GetInt32();

        _screens.Clear();

        foreach (var node in element.GetProperty("screens").EnumerateArray())
        {
            _screens.Add(new UiScreen(node, opt));
        }
    }

#if EDITOR
    private static readonly int Version = 1;

    public bool IsValidName(string name)
    {
        foreach (var screen in _screens)
        {
            if (screen.Name.Equals(name))
            {
                return false;
            }
        }

        return true;
    }

    public UiScreen GetScreen(string name)
    {
        foreach (var screen in _screens)
        {
            if (screen.Name.Equals(name))
            {
                return screen;
            }
        }

        throw new InvalidOperationException("Screenmanager.GetScreen() : can't find the screen " + name);
    }

    public void AddScreen(UiScreen screen)
    {
        _screens.Add(screen);
    }

    public void RemoveScreen(UiScreen screen)
    {
        _screens.Remove(screen);
    }

    public void RemoveScreen(string name)
    {
        UiScreen s = null;

        foreach (var screen in _screens)
        {
            if (screen.Name.Equals(name))
            {
                RemoveScreen(s);
                return;
            }
        }

        throw new InvalidOperationException("Screenmanager.RemoveScreen() : can't find the screen " + name);
    }

    public void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("version", Version);

        var screensNode = new JArray();

        foreach (var screen in _screens)
        {
            var screenObject = new JObject();

            screen.Save(screenObject, option);
            screensNode.Add(screenObject);
        }

        jObject.Add("screens", screensNode);
    }

#endif
}