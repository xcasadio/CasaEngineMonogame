using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Common;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Common;

/// <summary>
/// D-N-6: every type constructed by the converter gets an additive constructor taking a
/// deterministic <see cref="Guid"/> so Ids.For-derived ids survive into the written asset (see
/// docs/plan-nettoyage-convertisseur.md, decision D-N-6). This covers the ctor half of the
/// contract: <see cref="ObjectBase.Id"/> is assigned exactly the given id and
/// <see cref="ObjectBase.Name"/> is never left null (the default parameterless constructor's
/// "Object {guid}" convention, <see cref="ObjectBase"/> lines 19-23). <see cref="Load"/> still
/// overriding both afterwards is covered separately below.
/// </summary>
public class ObjectBaseGuidConstructorTests
{
    public static IEnumerable<object[]> GuidConstructedTypes()
    {
        yield return new object[] { "Entity", (Func<Guid, ObjectBase>)(id => new Entity(id)) };
        yield return new object[] { "SpriteData", (Func<Guid, ObjectBase>)(id => new SpriteData(id)) };
        yield return new object[] { "Animation2dData", (Func<Guid, ObjectBase>)(id => new Animation2dData(id)) };
        yield return new object[] { "Box", (Func<Guid, ObjectBase>)(id => new Box(id)) };
        yield return new object[] { "TransformComponent", (Func<Guid, ObjectBase>)(id => new TransformComponent(id)) };
        yield return new object[] { "RenderProjectionComponent", (Func<Guid, ObjectBase>)(id => new RenderProjectionComponent(id)) };
        yield return new object[] { "AnimatedSpriteComponent", (Func<Guid, ObjectBase>)(id => new AnimatedSpriteComponent(id)) };
        yield return new object[] { "CollisionComponent", (Func<Guid, ObjectBase>)(id => new CollisionComponent(id)) };
        yield return new object[] { "DepthSortable2DComponent", (Func<Guid, ObjectBase>)(id => new DepthSortable2DComponent(id)) };
        yield return new object[] { "CharacterControllerComponent", (Func<Guid, ObjectBase>)(id => new CharacterControllerComponent(id)) };
    }

    [Theory]
    [MemberData(nameof(GuidConstructedTypes))]
    public void GuidConstructor_AssignsId_AndNonNullName(string typeName, Func<Guid, ObjectBase> factory)
    {
        var id = Guid.NewGuid();

        var instance = factory(id);

        Assert.Equal(id, instance.Id);
        Assert.False(string.IsNullOrEmpty(instance.Name), $"{typeName}(Guid) must not leave Name null or empty.");
        Assert.NotEqual("Object " + Guid.Empty, instance.Name);
    }

    [Fact]
    public void GuidConstructor_DefaultsName_LikeParameterlessConstructor()
    {
        var id = Guid.NewGuid();

        var instance = new Entity(id);

        Assert.Equal("Object " + id, instance.Name);
    }

    [Fact]
    public void Load_StillOverrides_IdAndName_ForBox()
    {
        var box = new Box(Guid.NewGuid());
        var loadedId = Guid.NewGuid();
        var element = JObject.Parse($$"""
            {
              "id": "{{loadedId}}",
              "name": "loaded-box",
              "w": 1.0,
              "h": 2.0,
              "l": 3.0
            }
            """);

        box.Load(element);

        Assert.Equal(loadedId, box.Id);
        Assert.Equal("loaded-box", box.Name);
    }

    [Fact]
    public void Load_StillOverrides_IdAndName_ForEntity()
    {
        var entity = new Entity(Guid.NewGuid());
        var loadedId = Guid.NewGuid();
        var element = JObject.Parse($$"""
            {
              "id": "{{loadedId}}",
              "name": "loaded-entity",
              "script_class_name": null,
              "script": null,
              "root_component": null,
              "components": []
            }
            """);

        entity.Load(element);

        Assert.Equal(loadedId, entity.Id);
        Assert.Equal("loaded-entity", entity.Name);
    }
}
