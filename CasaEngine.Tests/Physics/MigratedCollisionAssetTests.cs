using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities.Components;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// The shipped projects were migrated from the typed collision components to <see cref="CollisionComponent"/>.
/// This loads every migrated component node exactly the way <see cref="ElementFactory"/> does at runtime.
/// </summary>
public class MigratedCollisionAssetTests
{
    public static TheoryData<string> MigratedAssets =>
    [
        @"Projects\SampleProject\DefaultWorld.world",
        @"Projects\SampleProject\Entities\Box.entity",
        @"Projects\RPGDemo\DefaultWorld.world",
        @"Projects\RPGDemo\Entities\character_link.entity",
        @"Projects\RPGDemo\Entities\character_octopus.entity",
        @"Projects\RPGDemo\Entities\weapon_rock.entity",
    ];

    [Theory]
    [MemberData(nameof(MigratedAssets))]
    public void MigratedAsset_LoadsItsCollisionComponents(string relativePath)
    {
        var path = Path.Combine(GetRepositoryRoot(), relativePath);
        Assert.True(File.Exists(path), path);

        var root = JToken.Parse(File.ReadAllText(path));
        var componentNodes = new List<JObject>();
        CollectCollisionComponentNodes(root, componentNodes);

        Assert.NotEmpty(componentNodes);

        foreach (var componentNode in componentNodes)
        {
            var component = ElementFactory.Load<EntityComponent>(componentNode);
            var collisionComponent = Assert.IsType<CollisionComponent>(component);

            Assert.NotEmpty(collisionComponent.Fixtures);

            foreach (var fixture in collisionComponent.Fixtures)
            {
                Assert.NotNull(fixture.Shape);
                Assert.True(fixture.Shape is Box or Sphere or Capsule or Cylinder);
            }
        }
    }

    [Fact]
    public void MigratedAssets_KeepNoTypedCollisionComponent()
    {
        string root = GetRepositoryRoot();
        //The typed collision components this phase deleted, by the shape they used to carry.
        string[] legacyShapeNames = ["Box", "Box2d", "Capsule", "Circle", "Cylinder", "Sphere"];

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "Projects"), "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".world", StringComparison.OrdinalIgnoreCase)
                && !file.EndsWith(".entity", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            foreach (var legacyShapeName in legacyShapeNames)
            {
                Assert.DoesNotContain(legacyShapeName + "CollisionComponent", content, StringComparison.Ordinal);
            }
        }
    }

    private static void CollectCollisionComponentNodes(JToken token, List<JObject> output)
    {
        if (token is JArray array)
        {
            foreach (var item in array)
            {
                CollectCollisionComponentNodes(item, output);
            }

            return;
        }

        if (token is not JObject node)
        {
            return;
        }

        if (node["type"]?.Value<string>() == nameof(CollisionComponent))
        {
            output.Add(node);
        }

        foreach (var property in node.Properties())
        {
            CollectCollisionComponentNodes(property.Value, output);
        }
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }
}
