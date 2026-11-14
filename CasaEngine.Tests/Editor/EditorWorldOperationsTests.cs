using System.ComponentModel;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>ADR-0092, task T2.1: the world edits shared by the panels and the remote commands, each undone and redone.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorWorldOperationsTests : IDisposable
{
    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.World, "editor-world-operations-tests");
    private readonly World _world = new() { Name = "TestWorld" };

    public EditorWorldOperationsTests()
    {
        EditorHistoryService.Current.IsSuspended = false;
        EditorHistoryService.Current.Remove(Context);
    }

    public void Dispose() => EditorHistoryService.Current.Remove(Context);

    private static EditorHistoryStack History => EditorHistoryService.Current.GetOrCreate(Context);

    private static void UndoRedo(Action assertDone, Action assertUndone)
    {
        assertDone();
        History.Undo();
        assertUndone();
        History.Redo();
        assertDone();
    }

    private Entity AddEntity(string name, Entity parent = null)
    {
        var entity = new Entity { Name = name };
        EditorWorldOperations.AddEntity(Context, _world, entity, parent, null);
        return entity;
    }

    [Fact]
    public void AddEntity_IsUndoable_AndCallsTheSelection()
    {
        Entity selected = null;
        var entity = new Entity { Name = "A" };

        EditorWorldOperations.AddEntity(Context, _world, entity, null, null, e => selected = e);

        Assert.Same(entity, selected);
        UndoRedo(() => Assert.Contains(entity, _world.Entities), () => Assert.DoesNotContain(entity, _world.Entities));
        Assert.Same(entity, selected);
    }

    [Fact]
    public void AddEntity_UnderAParent()
    {
        Entity parent = AddEntity("Parent");
        Entity child = AddEntity("Child", parent);

        UndoRedo(() => Assert.Same(parent, child.Parent), () => Assert.Null(child.Parent));
    }

    [Fact]
    public void DuplicateAndDelete_AreUndoable()
    {
        Entity source = AddEntity("Source");
        Entity duplicate = source.Clone();
        duplicate.Name = "Source Copy";

        EditorWorldOperations.DuplicateEntity(Context, _world, duplicate, null, source);
        UndoRedo(() => Assert.Contains(duplicate, _world.Entities), () => Assert.DoesNotContain(duplicate, _world.Entities));

        EditorWorldOperations.DeleteEntity(Context, _world, source);
        UndoRedo(() => Assert.DoesNotContain(source, _world.Entities), () => Assert.Contains(source, _world.Entities));
    }

    [Fact]
    public void Rename_IsUndoable_AndSameNameIsNoEntry()
    {
        Entity entity = AddEntity("Old");
        long revision = History.CurrentRevision;

        Assert.False(EditorWorldOperations.RenameEntity(Context, entity, "Old"));
        Assert.Equal(revision, History.CurrentRevision);

        Assert.True(EditorWorldOperations.RenameEntity(Context, entity, "New"));
        UndoRedo(() => Assert.Equal("New", entity.Name), () => Assert.Equal("Old", entity.Name));
    }

    [Fact]
    public void AddComponent_AsRoot_AsChild_AndInTheList()
    {
        Entity entity = AddEntity("E");
        var root = new TestSceneComponent();
        var child = new TestSceneComponent();
        var plain = new WorldOperationsTestComponent();

        EditorWorldOperations.AddComponent(Context, entity, root, ComponentPlacement.Root, null, null);
        UndoRedo(() => Assert.Same(root, entity.RootComponent), () => Assert.Null(entity.RootComponent));

        EditorWorldOperations.AddComponent(Context, entity, child, ComponentPlacement.Child, root, root);
        UndoRedo(() => Assert.Same(root, child.Parent), () => Assert.DoesNotContain(child, root.Children));

        EditorWorldOperations.AddComponent(Context, entity, plain, ComponentPlacement.List, null, null);
        UndoRedo(() => Assert.Contains(plain, entity.Components), () => Assert.DoesNotContain(plain, entity.Components));

        Assert.True(EditorWorldOperations.TryGetPlacement(entity, child, out ComponentPlacement placement, out SceneComponent parent));
        Assert.Equal(ComponentPlacement.Child, placement);
        Assert.Same(root, parent);
    }

    [Fact]
    public void AddComponent_RejectsAnInvalidPlacement()
    {
        Entity entity = AddEntity("E");
        EditorWorldOperations.AddComponent(Context, entity, new TestSceneComponent(), ComponentPlacement.Root, null, null);

        Assert.Throws<ArgumentException>(() => EditorWorldOperations.AddComponent(Context, entity, new TestSceneComponent(), ComponentPlacement.Root, null, null));
        Assert.Throws<ArgumentException>(() => EditorWorldOperations.AddComponent(Context, entity, new WorldOperationsTestComponent(), ComponentPlacement.Child, entity.RootComponent, null));
        Assert.Throws<ArgumentException>(() => EditorWorldOperations.AddComponent(Context, entity, new TestSceneComponent(), ComponentPlacement.Child, null, null));
    }

    [Fact]
    public void RemoveComponent_RootWithoutChildren_ThenUndoPutsItBack()
    {
        Entity entity = AddEntity("E");
        var root = new TestSceneComponent();
        EditorWorldOperations.AddComponent(Context, entity, root, ComponentPlacement.Root, null, null);

        Assert.True(EditorWorldOperations.TryRemoveComponent(Context, entity, root, out string error));
        Assert.Null(error);
        UndoRedo(() => Assert.Null(entity.RootComponent), () => Assert.Same(root, entity.RootComponent));
    }

    [Fact]
    public void RemoveComponent_WithChildren_IsRefused()
    {
        Entity entity = AddEntity("E");
        var root = new TestSceneComponent();
        EditorWorldOperations.AddComponent(Context, entity, root, ComponentPlacement.Root, null, null);
        EditorWorldOperations.AddComponent(Context, entity, new TestSceneComponent(), ComponentPlacement.Child, root, root);
        long revision = History.CurrentRevision;

        Assert.False(EditorWorldOperations.TryRemoveComponent(Context, entity, root, out string error));

        Assert.Contains("child", error);
        Assert.Equal(revision, History.CurrentRevision);
        Assert.Same(root, entity.RootComponent);
    }

    [Fact]
    public void RemoveComponent_Child_UndoPutsItBackUnderTheSameParent()
    {
        Entity entity = AddEntity("E");
        var root = new TestSceneComponent();
        var child = new TestSceneComponent();
        EditorWorldOperations.AddComponent(Context, entity, root, ComponentPlacement.Root, null, null);
        EditorWorldOperations.AddComponent(Context, entity, child, ComponentPlacement.Child, root, root);

        Assert.True(EditorWorldOperations.TryRemoveComponent(Context, entity, child, out _));

        UndoRedo(() => Assert.DoesNotContain(child, root.Children), () => Assert.Same(root, child.Parent));
    }

    [Fact]
    public void RemoveComponent_FromTheList_AndOfAnotherEntity()
    {
        Entity entity = AddEntity("E");
        Entity other = AddEntity("Other");
        var plain = new WorldOperationsTestComponent();
        EditorWorldOperations.AddComponent(Context, entity, plain, ComponentPlacement.List, null, null);

        Assert.False(EditorWorldOperations.TryRemoveComponent(Context, other, plain, out _));
        Assert.True(EditorWorldOperations.TryRemoveComponent(Context, entity, plain, out _));

        UndoRedo(() => Assert.DoesNotContain(plain, entity.Components), () => Assert.Contains(plain, entity.Components));
    }

    [Fact]
    public void SetProperty_IsUndoable_AndEqualValueIsNoEntry()
    {
        var target = new WorldOperationsTestComponent { Speed = 1f };
        PropertyDescriptor speed = TypeDescriptor.GetProperties(target)[nameof(WorldOperationsTestComponent.Speed)]!;
        int applied = 0;

        Assert.False(EditorWorldOperations.SetProperty(Context, "Speed", target, speed, 1f, () => applied++));
        Assert.True(EditorWorldOperations.SetProperty(Context, "Speed", target, speed, 4f, () => applied++));

        UndoRedo(() => Assert.Equal(4f, target.Speed), () => Assert.Equal(1f, target.Speed));
        Assert.Equal(3, applied);
    }

    [Fact]
    public void ComponentTypes_OnlyCreateAllowedTypes()
    {
        Assert.True(EditorWorldOperations.TryCreateComponent(nameof(WorldOperationsTestComponent), out EntityComponent created));
        Assert.IsType<WorldOperationsTestComponent>(created);

        Assert.False(EditorWorldOperations.TryCreateComponent("Process", out _));
        Assert.False(EditorWorldOperations.TryCreateComponent(nameof(WorldOperationsHiddenComponent), out _));
        Assert.False(EditorWorldOperations.TryCreateComponent(nameof(EntityComponent), out _));
        Assert.False(EditorWorldOperations.TryCreateComponent(string.Empty, out _));
    }

    public sealed class WorldOperationsTestComponent : EntityComponent
    {
        public float Speed { get; set; }

        public override WorldOperationsTestComponent Clone() => new() { Speed = Speed };
    }

    [Browsable(false)]
    public sealed class WorldOperationsHiddenComponent : EntityComponent
    {
        public override WorldOperationsHiddenComponent Clone() => new();
    }

    public sealed class TestSceneComponent : SceneComponent
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
