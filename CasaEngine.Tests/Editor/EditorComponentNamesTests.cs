using CasaEngine.Editor.Controls;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>The editor names the components it adds after their type, and the inspector label hides that default.</summary>
public sealed class EditorComponentNamesTests
{
    [Fact]
    public void CreateUniqueName_OnAnEmptyEntity_IsTheTypeName()
    {
        var entity = new Entity();

        var name = EditorComponentNames.CreateUniqueName(entity, new StaticModelComponent());

        Assert.Equal("StaticModelComponent", name);
    }

    [Fact]
    public void CreateUniqueName_WhenTheRootComponentHasTheName_AddsTwo()
    {
        var entity = new Entity();
        entity.RootComponent = NamedByType(new StaticModelComponent(), entity);

        var name = EditorComponentNames.CreateUniqueName(entity, new StaticModelComponent());

        Assert.Equal("StaticModelComponent (2)", name);
    }

    [Fact]
    public void CreateUniqueName_CountsTheChildrenOfTheRoot_AndGoesOnWithThree()
    {
        var entity = new Entity();
        var root = new StaticModelComponent();
        entity.RootComponent = root;
        var firstChild = new StaticModelComponent();
        root.Children.Add(firstChild);
        firstChild.Name = EditorComponentNames.CreateUniqueName(entity, firstChild);
        var secondChild = new StaticModelComponent();
        root.Children.Add(secondChild);
        secondChild.Name = EditorComponentNames.CreateUniqueName(entity, secondChild);
        root.Name = "Root";

        Assert.Equal("StaticModelComponent", firstChild.Name);
        Assert.Equal("StaticModelComponent (2)", secondChild.Name);
        Assert.Equal("StaticModelComponent (3)", EditorComponentNames.CreateUniqueName(entity, new StaticModelComponent()));
    }

    [Fact]
    public void CreateUniqueName_CountsTheComponentsOfTheEntity_AndGoesOnWithThree()
    {
        var entity = new Entity();
        var first = new PerspectiveCameraComponent { Name = "PerspectiveCameraComponent" };
        var second = new PerspectiveCameraComponent { Name = "PerspectiveCameraComponent (2)" };
        entity.AddComponent(first);
        entity.AddComponent(second);

        var name = EditorComponentNames.CreateUniqueName(entity, new PerspectiveCameraComponent());

        Assert.Equal("PerspectiveCameraComponent (3)", name);
    }

    [Fact]
    public void CreateUniqueName_TreatsADifferentCaseAsTaken()
    {
        var entity = new Entity();
        entity.AddComponent(new PerspectiveCameraComponent { Name = "perspectivecameracomponent" });

        var name = EditorComponentNames.CreateUniqueName(entity, new PerspectiveCameraComponent());

        Assert.Equal("PerspectiveCameraComponent (2)", name);
    }

    [Fact]
    public void GetComponentLabel_WithTheTypeNameAsInstanceName_IsTheDisplayNameAlone()
    {
        var component = new StaticModelComponent { Name = "StaticModelComponent" };

        Assert.Equal("Static Model", EntityDetailsPanel.GetComponentLabel(component));
    }

    [Fact]
    public void GetComponentLabel_WithANumberedName_AddsTheNameInBrackets()
    {
        var component = new StaticModelComponent { Name = "StaticModelComponent (2)" };

        Assert.Equal("Static Model [StaticModelComponent (2)]", EntityDetailsPanel.GetComponentLabel(component));
    }

    [Fact]
    public void GetComponentLabel_WithTheEngineDefaultName_IsTheDisplayNameAlone()
    {
        var component = new StaticModelComponent();
        Assert.StartsWith("Object ", component.Name);

        Assert.Equal("Static Model", EntityDetailsPanel.GetComponentLabel(component));
    }

    private static T NamedByType<T>(T component, Entity entity) where T : EntityComponent
    {
        component.Name = EditorComponentNames.CreateUniqueName(entity, component);
        return component;
    }
}
