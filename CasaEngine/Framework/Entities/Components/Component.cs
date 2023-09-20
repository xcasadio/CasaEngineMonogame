using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CasaEngine.Core.Design;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public abstract class Component : ISaveLoad
#if EDITOR
    , INotifyPropertyChanged
#endif
{
    public Entity Owner { get; private set; }
    public abstract int ComponentId { get; }
    public bool IsInitialized { get; private set; }

    public virtual void Initialize(Entity entity)
    {
        IsInitialized = true;
        Owner = entity;
    }

    public abstract void Update(float elapsedTime);

    public virtual void Draw()
    {
    }

    public abstract Component Clone();

    public abstract void Load(JsonElement element, SaveOption option);

    public virtual void ScreenResized(int width, int height)
    {
        //do nothing
    }
    public virtual void OnEnabledValueChange()
    {
        //do nothing
    }

#if EDITOR
    public string? DisplayName => GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public virtual void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("version", 1);
        jObject.Add("type", ComponentId);
    }
#endif
}