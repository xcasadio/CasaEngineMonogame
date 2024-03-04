using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.GUI.Neoforce;

public class Component : Disposable
{
    private bool _initialized;

    public Manager? Manager { get; private set; }

    public bool Initialized => _initialized;

    protected Component()
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
        }
        base.Dispose(disposing);
    }

    public virtual void Initialize(Manager manager)
    {
        Manager = manager;
        _initialized = true;
    }

    protected internal virtual void Update(GameTime gameTime)
    {
    }
}