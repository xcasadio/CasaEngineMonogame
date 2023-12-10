using System;
using Microsoft.Xna.Framework;

namespace TomShane.Neoforce.Controls;

public class Component : Disposable
{
    private Manager _manager;
    private bool _initialized;

    public virtual Manager Manager
    {
        get => _manager;
        set => _manager = value;
    }
    public virtual bool Initialized => _initialized;

    public Component(Manager manager)
    {
        if (manager != null)
        {
            _manager = manager;
        }
        else
        {
            Manager.Logger.WriteLineError("Component cannot be created. Manager instance is needed.");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
        }
        base.Dispose(disposing);
    }

    public virtual void Init()
    {
        _initialized = true;
    }

    protected internal virtual void Update(GameTime gameTime)
    {
    }

}