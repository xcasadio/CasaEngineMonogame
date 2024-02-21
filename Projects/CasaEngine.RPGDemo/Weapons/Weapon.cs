using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers;

namespace CasaEngine.RPGDemo.Weapons;

public abstract class Weapon
{
    protected World World { get; }
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

    protected Weapon(World world, Entity entity)
    {
        World = world;
        Entity = entity;
    }

    protected abstract void Initialize();

    public abstract void Attach();

    public abstract void UnAttachWeapon();
}