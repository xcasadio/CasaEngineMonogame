using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.Framework.Assets;

public static class ElementFactory
{
    public static T Create<T>(string typeName) where T : class
    {
        var type = FindTypeByName(typeName);
        return Activator.CreateInstance(type) as T;
    }

    public static T Load<T>(JsonElement element) where T : class, ISerializable
    {
        var typeName = string.Empty;

        if (element.GetProperty("type").ValueKind == JsonValueKind.Number)
        {
            int typeId = element.GetProperty("type").GetInt32();

            switch (typeId)
            {
                //case 1: //GamePlay
                //    typeName = "";
                //    break;
                case 2: //Mesh
                    typeName = nameof(StaticMeshComponent);
                    break;
                case 3: //ArcBallCamera
                    typeName = nameof(ArcBallCameraComponent);
                    break;
                case 4: //Physics2d
                    typeName = element.GetProperty("shape").GetProperty("shape_type").GetString() == "Circle" ? nameof(CircleCollisionComponent) : nameof(Box2dCollisionComponent);
                    break;
                case 5: //Physics
                    typeName = nameof(BoxCollisionComponent);
                    break;
                case 6: //AnimatedSprite
                    typeName = nameof(AnimatedSpriteComponent);
                    break;
                case 7: //StaticSprite
                    typeName = nameof(StaticSpriteComponent);
                    break;
                case 8: //TileMap
                    typeName = nameof(TileMapComponent);
                    break;
                case 9: //LookAtCamera
                    typeName = nameof(CameraLookAtComponent);
                    break;
                case 10: //CameraTarget2d
                    typeName = nameof(CameraTargeted2dComponent);
                    break;
                case 11: //Camera3dIn2dAxis
                    typeName = nameof(Camera3dIn2dAxisComponent);
                    break;
                case 12: //SkinnedMesh
                    typeName = nameof(SkinnedMeshComponent);
                    break;
                case 13: //PlayerStart
                    typeName = nameof(PlayerStartComponent);
                    break;
                default:
                    throw new InvalidDataException($"Component type {typeId} not supported");
            }
        }
        else
        {
            typeName = element.GetProperty("type").GetString();
        }

        var component = Create<T>(typeName);
        component.Load(element);
        return component;
    }

    private static Type? FindTypeByName(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .FirstOrDefault(x => string.Equals(x.Name, typeName, StringComparison.InvariantCultureIgnoreCase));
    }

#if EDITOR

    public static IEnumerable<Type> GetDerivedTypesFrom<T>() where T : class
    {
        var type = typeof(T);
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => x is { IsClass: true, IsGenericType: false, IsInterface: false, IsAbstract: false }
                        && x.IsSubclassOf(type));
    }

#endif
}