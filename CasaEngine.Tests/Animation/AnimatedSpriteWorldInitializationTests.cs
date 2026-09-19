using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// Covers how <see cref="AnimatedSpriteComponent.InitializeWithWorld"/> rebuilds
/// <see cref="AnimatedSpriteComponent.Animations"/> for the world the component enters. It used to
/// rebuild them from the serialized animation asset ids alone, which silently dropped every animation
/// handed over by <see cref="AnimatedSpriteComponent.AddAnimation"/> - the way the demos, the editor
/// preview and any sprite built in code populate the component. Such a component entered the world
/// with no animation at all and a null <c>CurrentAnimation</c>, and <c>TileMapDemo</c>'s
/// <c>PlayerComponent</c> threw a <see cref="NullReferenceException"/> on its very first frame.
/// </summary>
public class AnimatedSpriteWorldInitializationTests
{
    [Fact]
    public void AnimationAddedInCode_SurvivesInitializeWithWorld()
    {
        var component = CreateComponentInWorld(out var world);
        component.AddAnimation(new Animation2d(CreateAnimationData("swordman_stand_right")));

        component.InitializeWithWorld(world);

        var animation = Assert.Single(component.Animations);
        Assert.Equal("swordman_stand_right", animation.Animation2dData.Name);
        Assert.Same(animation, component.CurrentAnimation);

        // Without a composition sampler nothing advances and nothing draws, so a non-null
        // CurrentAnimation on its own would not prove the animation is usable.
        Assert.NotNull(GetCurrentSampler(component));
    }

    [Fact]
    public void SecondInitializeWithWorld_KeepsTheAnimationOnce_AndRebindsItsSampler()
    {
        var component = CreateComponentInWorld(out var world);
        component.AddAnimation(new Animation2d(CreateAnimationData("swordman_stand_right")));

        component.InitializeWithWorld(world);
        var firstSampler = GetCurrentSampler(component);
        Assert.NotNull(firstSampler);

        component.InitializeWithWorld(world);

        // Re-entering a world registers the same animation once more, not twice.
        Assert.Single(component.Animations);
        Assert.NotNull(component.CurrentAnimation);

        // The samplers of the world just left are dropped, so the rebuilt animation gets a fresh one:
        // keeping CurrentAnimation across the rebuild would send SetCurrentAnimation down its
        // same-name shortcut and leave the component with no sampler at all.
        var secondSampler = GetCurrentSampler(component);
        Assert.NotNull(secondSampler);
        Assert.NotSame(firstSampler, secondSampler);
    }

    private static Animation2dData CreateAnimationData(string name)
    {
        var animationData = new Animation2dData { Name = name };
        animationData.Parts.Add(new Animation2dPartData { Id = "sprite" });
        return animationData;
    }

    private static AnimatedSpriteComponent CreateComponentInWorld(out CasaEngine.Framework.Scene.World.World world)
    {
        world = new CasaEngine.Framework.Scene.World.World();

        // RuntimeHelpers.GetUninitializedObject skips Game's constructor, so Components (backed by
        // _components) is otherwise null: AnimatedSpriteComponent.InitializeWithWorld looks up a
        // SpriteRendererComponent through it. AssetContentManager stays null, which is exactly the
        // state of a component that names no animation asset id.
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());
        SetProperty(world, nameof(CasaEngine.Framework.Scene.World.World.Game), game);

        var entityRoot = new TestSceneComponent();
        var entity = new Entity { RootComponent = entityRoot };
        SetProperty(entity, nameof(Entity.World), world);

        var component = new AnimatedSpriteComponent();
        entityRoot.AddChildComponent(component);
        return component;
    }

    private static Animation2dCompositionSampler? GetCurrentSampler(AnimatedSpriteComponent component)
    {
        var field = typeof(AnimatedSpriteComponent).GetField("_currentCompositionSampler", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (Animation2dCompositionSampler?)field!.GetValue(component);
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class TestSceneComponent : SceneComponent
    {
        public TestSceneComponent()
        {
        }

        private TestSceneComponent(TestSceneComponent other) : base(other)
        {
        }

        public override TestSceneComponent Clone() => new(this);
    }
}
