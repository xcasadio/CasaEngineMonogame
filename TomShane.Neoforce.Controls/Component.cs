using Microsoft.Xna.Framework;

namespace TomShane.Neoforce.Controls;

public class Component : Disposable
{
    private bool _initialized;

    public Manager Manager { get; set; }

    public bool Initialized => _initialized;

    protected Component(Manager manager)
    {
        Manager = manager;
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