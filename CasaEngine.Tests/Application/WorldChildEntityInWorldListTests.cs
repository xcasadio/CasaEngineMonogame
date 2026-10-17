using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Application;

/// <summary>
/// ADR-0076: an entity lives either in a world's entity list or under a parent entity, never in both places.
///
/// RPGDemo broke that rule: ScriptPlayer spawned weapon_sword into the world (World.SpawnEntity), then
/// MeleeWeapon made it a child of character_link (Entity.AddChild). The sword was then updated twice per
/// frame (by the world and by its parent), and a world reload after the player died crashed:
/// World.ClearEntities detached the parent with its children, then met the sword further down its list and
/// called Entity.Destroy on a detached root (NullReferenceException in AnimatedSpriteComponent).
/// </summary>
public class WorldChildEntityInWorldListTests
{
    private sealed class UpdateCountingComponent : EntityComponent
    {
        public int Updates;

        public override void Update(float elapsedTime)
        {
            Updates++;
            base.Update(elapsedTime);
        }

        public override EntityComponent Clone() => new UpdateCountingComponent();
    }

    [Fact]
    public void AddChild_OfAnEntityWaitingToBeAddedToTheWorld_Throws_AndChangesNothing()
    {
        var world = CreateWorld();
        var parent = AddParentToWorld(world);
        var child = new Entity { Name = "weapon_sword", RootComponent = new LightComponent() };
        world.AddEntity(child);

        var exception = Assert.Throws<InvalidOperationException>(() => parent.AddChild(child));

        Assert.Contains("weapon_sword", exception.Message);
        Assert.Contains("character_link", exception.Message);
        Assert.Empty(parent.Children);
        Assert.Null(child.Parent);
    }

    [Fact]
    public void AddChild_OfAnEntityInTheWorldList_Throws()
    {
        var world = CreateWorld();
        var parent = AddParentToWorld(world);
        var child = new Entity { Name = "weapon_sword", RootComponent = new LightComponent() };
        world.AddEntity(child);
        world.Update(1f / 60f);
        Assert.Contains(child, world.Entities);

        Assert.Throws<InvalidOperationException>(() => parent.AddChild(child));

        Assert.Empty(parent.Children);
        Assert.Null(child.Parent);
    }

    [Fact]
    public void AddEntity_OfAChildEntity_Throws_AndQueuesNothing()
    {
        var world = CreateWorld();
        var parent = AddParentToWorld(world);
        var child = new Entity { Name = "weapon_sword", RootComponent = new LightComponent() };
        parent.AddChild(child);

        var exception = Assert.Throws<InvalidOperationException>(() => world.AddEntity(child));

        Assert.Contains("weapon_sword", exception.Message);
        Assert.Contains("character_link", exception.Message);
        world.Update(1f / 60f);
        Assert.DoesNotContain(child, world.Entities);
        Assert.Same(parent, child.Parent);
    }

    [Fact]
    public void AddEntityReference_OfAChildEntity_Throws()
    {
        var world = CreateWorld();
        var parent = AddParentToWorld(world);
        var child = new Entity { Name = "weapon_sword", RootComponent = new LightComponent() };
        parent.AddChild(child);

        Assert.Throws<InvalidOperationException>(() =>
            EditorWorldEditingService.AddEntityReference(world, new EntityReference { AssetId = Guid.Empty, Entity = child }));

        Assert.DoesNotContain(child, world.Entities);
    }

    /// <summary>The way RPGDemo now attaches its sword: created outside the world, then made a child of an
    /// entity of the world, which initializes it.</summary>
    [Fact]
    public void AChildCreatedOutsideTheWorld_IsInitializedByTheWorld_UpdatedOncePerWorldUpdate_AndClearedWithoutError()
    {
        var world = CreateWorld();
        var parent = AddParentToWorld(world);
        var counter = new UpdateCountingComponent();
        var child = new Entity { Name = "weapon_sword", RootComponent = new AnimatedSpriteComponent() };
        child.PolicySourceMode = EntityPolicySourceMode.Explicit;
        child.TickPolicy = TickPolicy.EveryFrame;
        child.AddComponent(counter);

        parent.AddChild(child);

        Assert.True(child.IsInitialized);
        Assert.Same(world, child.World);
        Assert.DoesNotContain(child, world.Entities);

        int before = counter.Updates;
        world.Update(1f / 60f);
        Assert.Equal(1, counter.Updates - before);

        var exception = Record.Exception(() => world.ClearEntities());
        Assert.Null(exception);
    }

    [Fact]
    public void AnEntityDestroyedAndRemovedByTheWorld_CanBecomeAChild()
    {
        var world = CreateWorld();
        var parent = AddParentToWorld(world);
        var entity = new Entity { Name = "weapon_sword", RootComponent = new LightComponent() };
        world.AddEntity(entity);
        world.Update(1f / 60f);

        entity.Destroy();
        world.Update(1f / 60f);
        Assert.DoesNotContain(entity, world.Entities);

        AssertCanBecomeAChild(parent, entity);
    }

    [Fact]
    public void AnEntityRemovedByTheEditor_CanBecomeAChild()
    {
        var world = CreateWorld();
        var parent = AddParentToWorld(world);
        var entity = new Entity { Name = "weapon_sword", RootComponent = new LightComponent() };
        EditorWorldEditingService.AddEntityReference(world, new EntityReference { AssetId = Guid.Empty, Entity = entity });

        EditorWorldEditingService.RemoveEntity(world, entity);

        AssertCanBecomeAChild(parent, entity);
    }

    [Fact]
    public void AnEntityStillWaitingWhenTheWorldIsCleared_CanBecomeAChildOfAnotherWorld()
    {
        var world = CreateWorld();
        var entity = new Entity { Name = "weapon_sword", RootComponent = new LightComponent() };
        world.AddEntity(entity);

        world.ClearEntities();

        var otherWorld = CreateWorld();
        var parent = AddParentToWorld(otherWorld);
        AssertCanBecomeAChild(parent, entity);
    }

    private static void AssertCanBecomeAChild(Entity parent, Entity entity)
    {
        entity.ReActivate();
        parent.AddChild(entity);
        Assert.Same(parent, entity.Parent);
        Assert.Contains(entity, parent.Children);
    }

    /// <summary>The parent ticks every frame, as character_link does in RPGDemo (its animated sprite always
    /// holds an animation), so it updates its children.</summary>
    private static Entity AddParentToWorld(World world)
    {
        var parent = new Entity { Name = "character_link", RootComponent = new LightComponent() };
        parent.PolicySourceMode = EntityPolicySourceMode.Explicit;
        parent.TickPolicy = TickPolicy.EveryFrame;
        EditorWorldEditingService.AddEntityReference(world, new EntityReference { AssetId = Guid.Empty, Entity = parent });
        return parent;
    }

    private static World CreateWorld()
    {
        var world = new World { Name = "ChildInWorldListWorld" };

        // RuntimeHelpers.GetUninitializedObject skips Game's constructor: Components (backed by _components)
        // would be null, and AnimatedSpriteComponent.InitializeWithWorld looks up a SpriteRendererComponent
        // through it.
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        //Likewise, GameManager (whose ViewManager World.Update reaches for on-demand view invalidation)
        //is only ever assigned in CasaEngineGame's constructor.
        var gameManager = (GameManager)RuntimeHelpers.GetUninitializedObject(typeof(GameManager));
        var viewManagerField = typeof(GameManager).GetField("<ViewManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        viewManagerField.SetValue(gameManager, new CasaEngine.Framework.Rendering.ViewManager());
        var gameManagerField = typeof(CasaEngineGame).GetField("<GameManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        gameManagerField.SetValue(game, gameManager);

        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;
        SetProperty(world, nameof(World.Game), game);
        return world;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }
}
