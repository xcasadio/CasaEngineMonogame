﻿using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CasaEngine.Framework.Game;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

public abstract class Component
#if EDITOR
    : INotifyPropertyChanged
#endif
{
    public Entity Owner { get; }
    public int Type { get; }

    protected Component(Entity entity, int type)
    {
        Owner = entity;
        Type = type;
    }

    public virtual void Initialize(CasaEngineGame game)
    {

    }

    public abstract void Update(float elapsedTime);

    public virtual void Draw()
    {
    }

    public bool HandleMessage(Message message)
    {
        return false;
    }

    public abstract void Load(JsonElement element);

    public virtual void ScreenResized(int width, int height)
    {

    }

    public abstract Component Clone(Entity owner);


#if EDITOR
    public string? DisplayName => GetType().GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public virtual void Save(JObject jObject)
    {
        jObject.Add("version", 1);
        jObject.Add("type", Type);
    }
#endif
}