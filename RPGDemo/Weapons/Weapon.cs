using CasaEngine.Framework.Entities;
using RPGDemo.Controllers;

namespace RPGDemo.Weapons;

public abstract class Weapon
{
    private Character _character;

    public Entity Entity { get; }

    public Character Character
    {
        get => _character;
        set
        {
            _character = value;
            Initialize();
        }
    }

    protected Weapon(Entity entity)
    {
        Entity = entity;
    }

    protected abstract void Initialize();

    public abstract void Attach();

    public abstract void UnAttachWeapon();
}