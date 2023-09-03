using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using RPGDemo.Controllers;

namespace RPGDemo.Weapons;

public abstract class Weapon
{
    protected CasaEngineGame Game { get; }
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

    protected Weapon(CasaEngineGame game, Entity entity)
    {
        Game = game;
        Entity = entity;
    }

    protected abstract void Initialize();

    public abstract void Attach();

    public abstract void UnAttachWeapon();
}