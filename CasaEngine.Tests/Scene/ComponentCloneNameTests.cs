using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace CasaEngine.Tests.Scene;

public class ComponentCloneNameTests
{
    /// <summary>Clones through the copy constructor, like CameraLookAtComponent.</summary>
    private sealed class CopyConstructorComponent : SceneComponent
    {
        public CopyConstructorComponent()
        {
        }

        private CopyConstructorComponent(CopyConstructorComponent other) : base(other)
        {
        }

        public override EntityComponent Clone()
        {
            return new CopyConstructorComponent(this);
        }
    }

    /// <summary>Clones by building a new object, like a component without a copy constructor.</summary>
    private sealed class NewObjectComponent : SceneComponent
    {
        public override EntityComponent Clone()
        {
            return new NewObjectComponent();
        }
    }

    private sealed class PlainComponent : EntityComponent
    {
        public override EntityComponent Clone()
        {
            return new PlainComponent();
        }
    }

    [Fact]
    public void EntityClone_KeepsTheNamesOfRootChildAndPlainComponents()
    {
        var entity = new Entity();
        var root = new CopyConstructorComponent { Name = "Root" };
        root.AddChildComponent(new NewObjectComponent { Name = "Child" });
        entity.RootComponent = root;
        entity.AddComponent(new PlainComponent { Name = "Plain" });
        entity.AddComponent(new CopyConstructorComponent { Name = "Copied" });

        var clone = entity.Clone();

        Assert.Equal("Root", clone.RootComponent.Name);
        Assert.NotEqual(root.Id, clone.RootComponent.Id);
        Assert.Equal("Child", clone.RootComponent.Children[0].Name);
        Assert.NotEqual(root.Children[0].Id, clone.RootComponent.Children[0].Id);
        Assert.Equal(new[] { "Plain", "Copied" }, clone.Components.Select(c => c.Name).ToArray());
        Assert.All(clone.Components.Zip(entity.Components), pair => Assert.NotEqual(pair.Second.Id, pair.First.Id));
    }
}
