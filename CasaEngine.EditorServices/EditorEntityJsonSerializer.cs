using System.Reflection;
using System.ComponentModel;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Engine.Geometry;

using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.EditorServices;

internal static class EditorEntityJsonSerializer
{
    private static readonly FieldInfo? WorldEntityReferencesField = typeof(World).GetField("_entityReferences", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? StaticSpriteDataField = typeof(StaticSpriteComponent).GetField("_spriteData", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void SaveWorld(World world, JObject node)
    {
        EditorJsonSaveHelper.SaveObjectBase(world, node);

        var entityReferencesArray = new JArray();
        foreach (var entityReference in GetEntityReferences(world))
        {
            if (entityReference.Entity.RootComponent != null)
            {
                entityReference.InitialLocalTransform.CopyFrom(entityReference.Entity.RootComponent.LocalTransform);
            }

            var entityReferenceNode = new JObject();
            SaveEntityReference(entityReference, entityReferenceNode);
            entityReferencesArray.Add(entityReferenceNode);
        }

        node.Add("entity_references", entityReferencesArray);
        node.Add("script_class_name", world.GameplayProxyClassName);
        node.Add("space_policy", world.SpacePolicyName);
        node.Add("player_startup_settings_asset_id", world.PlayerStartupSettingsAssetId);
        node.Add("gameplay_mode_asset_id", world.GameplayModeAssetId);

        var environmentNode = new JObject();
        world.EnvironmentSettings.Save(environmentNode);
        node.Add("environment", environmentNode);
    }

    public static void SaveEntity(Entity entity, JObject node)
    {
        EditorJsonSaveHelper.SaveObjectBase(entity, node);
        node.Add("policy_source", entity.Policies.PolicySourceMode.ToString());
        node.Add("mobility", entity.Policies.Mobility.ToString());
        node.Add("tick_policy", entity.Policies.TickPolicy.ToString());
        node.Add("spatial_policy", entity.Policies.SpatialPolicy.ToString());
        node.Add("render_dynamic_policy", entity.Policies.RenderDynamicPolicy.ToString());

        if (entity.RootComponent != null && ShouldSaveComponent(entity.RootComponent))
        {
            var rootComponentNode = new JObject();
            SaveComponent(entity.RootComponent, rootComponentNode);
            node.Add("root_component", rootComponentNode);
        }
        else
        {
            node.Add("root_component", "null");
        }

        var componentsArray = new JArray();
        foreach (var component in entity.Components)
        {
            if (!ShouldSaveComponent(component))
            {
                continue;
            }

            var componentNode = new JObject();
            SaveComponent(component, componentNode);
            componentsArray.Add(componentNode);
        }

        node.Add("components", componentsArray);
        node.Add("script_class_name", entity.GameplayProxyClassName);
    }

    internal static void SaveCollision2d(Collision2d collision2d, JObject node)
    {
        node.Add("collision_profile", collision2d.ProfileName ?? string.Empty);
        node.Add("tag", collision2d.Tag ?? string.Empty);

        var locationNode = new JObject();
        collision2d.LocalPosition.Save(locationNode);
        node.Add("location", locationNode);
        node.Add("orientation", collision2d.Rotation);

        SaveShape2d(collision2d.Shape, node);
    }

    internal static void SaveShape2d(Shape2d shape, JObject node)
    {
        node.Add("shape_type", shape.Type.ConvertToString());

        switch (shape)
        {
            case ShapeCircle circle:
                node.Add("radius", circle.Radius);
                break;

            case ShapeRectangle rectangle:
                node.Add("w", rectangle.Width);
                node.Add("h", rectangle.Height);
                break;
        }
    }

    internal static void SaveShape3d(Shape3d shape, JObject node)
    {
        EditorJsonSaveHelper.SaveObjectBase(shape, node);
        node.Add("shape_type", shape.Type.ConvertToString());

        switch (shape)
        {
            case Box box:
                node.Add("w", box.Size.X);
                node.Add("h", box.Size.Y);
                node.Add("l", box.Size.Z);
                break;

            case Capsule capsule:
                node.Add("radius", capsule.Radius);
                node.Add("length", capsule.Length);
                break;

            case Cylinder cylinder:
                node.Add("radius", cylinder.Radius);
                node.Add("length", cylinder.Length);
                break;

            case Sphere sphere:
                node.Add("radius", sphere.Radius);
                break;
        }
    }

    private static IReadOnlyList<EntityReference> GetEntityReferences(World world)
    {
        return WorldEntityReferencesField?.GetValue(world) as IReadOnlyList<EntityReference> ?? Array.Empty<EntityReference>();
    }

    private static void SaveEntityReference(EntityReference entityReference, JObject node)
    {
        node.Add("asset_id", entityReference.AssetId);

        if (entityReference.AssetId == Guid.Empty)
        {
            var entityNode = new JObject();
            SaveEntity(entityReference.Entity, entityNode);
            node.Add("entity", entityNode);
        }
        else
        {
            node.Add("name", entityReference.Name);

            var initialLocalTransformNode = new JObject();
            entityReference.InitialLocalTransform.Save(initialLocalTransformNode);
            node.Add("initial_local_transform", initialLocalTransformNode);
        }
    }

    private static void SaveComponent(EntityComponent component, JObject node)
    {
        switch (component)
        {
            case ArcBallCameraComponent arcBallCameraComponent:
                SaveArcBallCameraComponent(arcBallCameraComponent, node);
                return;

            case AnimatedSpriteComponent animatedSpriteComponent:
                SaveAnimatedSpriteComponent(animatedSpriteComponent, node);
                return;

            case Camera2dComponent camera2dComponent:
                SaveCamera2dComponent(camera2dComponent, node);
                return;

            case Camera3dComponent camera3dComponent:
                SaveCamera3dComponent(camera3dComponent, node);
                return;

            case CameraComponent cameraComponent:
                SaveCameraComponent(cameraComponent, node);
                return;

            case CollisionComponent collisionComponent:
                SaveCollisionComponent(collisionComponent, node);
                return;

            case LightComponent lightComponent:
                SaveLightComponent(lightComponent, node);
                return;

            case SkinnedMeshComponent skinnedMeshComponent:
                SaveSkinnedMeshComponent(skinnedMeshComponent, node);
                return;

            case PhysicsBaseComponent physicsBaseComponent:
                SavePhysicsBaseComponent(physicsBaseComponent, node);
                return;

            case ParticleSystemComponent particleSystemComponent:
                SaveParticleSystemComponent(particleSystemComponent, node);
                return;

            case StaticModelComponent staticModelComponent:
                SaveStaticModelComponent(staticModelComponent, node);
                return;

            case StaticSpriteComponent staticSpriteComponent:
                SaveStaticSpriteComponent(staticSpriteComponent, node);
                return;

            case TileMapComponent tileMapComponent:
                SaveTileMapComponent(tileMapComponent, node);
                return;

            case SceneComponent sceneComponent:
                SaveSceneComponent(sceneComponent, node);
                return;

            default:
                SaveEntityComponent(component, node);
                return;
        }
    }

    private static void SaveEntityComponent(EntityComponent component, JObject node)
    {
        EditorJsonSaveHelper.SaveObjectBase(component, node);
        node.Add("type", component.GetType().Name);
    }

    private static void SaveSceneComponent(SceneComponent component, JObject node)
    {
        SaveEntityComponent(component, node);

        var coordinatesNode = new JObject();
        component.LocalTransform.Save(coordinatesNode);
        node.Add("local_transform", coordinatesNode);

        var childrenArray = new JArray();
        foreach (var child in component.Children)
        {
            if (!ShouldSaveComponent(child))
            {
                continue;
            }

            var childNode = new JObject();
            SaveComponent(child, childNode);
            childrenArray.Add(childNode);
        }

        node.Add("children_component", childrenArray);
    }

    private static void SaveCameraComponent(CameraComponent component, JObject node)
    {
        SaveSceneComponent(component, node);

        node.Add("view_distance", component.ViewDistance);

        var viewportNode = new JObject();
        component.Viewport.Save(viewportNode);
        node.Add("viewport", viewportNode);
    }

    private static void SaveCamera2dComponent(Camera2dComponent component, JObject node)
    {
        SaveCameraComponent(component, node);

        var targetNode = new JObject();
        component.Target.Save(targetNode);
        node.Add("target", targetNode);
        node.Add("zoom", component.Zoom);
        node.Add("pixel_snap", component.PixelSnap);
    }

    private static void SaveCamera3dComponent(Camera3dComponent component, JObject node)
    {
        SaveCameraComponent(component, node);
        node.Add("fieldOfView", component.FieldOfView);
    }

    private static void SaveArcBallCameraComponent(ArcBallCameraComponent component, JObject node)
    {
        SaveCamera3dComponent(component, node);

        var targetNode = new JObject();
        component.Target.Save(targetNode);
        node.Add("target", targetNode);
        node.Add("distance", component.Distance);
        node.Add("yaw", component.Yaw);
        node.Add("pitch", component.Pitch);
    }

    private static void SaveAnimatedSpriteComponent(AnimatedSpriteComponent component, JObject node)
    {
        SaveSceneComponent(component, node);

        var colorNode = new JObject();
        component.Color.Save(colorNode);
        node.Add("color", colorNode);
        node.Add("sprite_effect", component.SpriteEffect.ConvertToString());

        var animationsArray = new JArray();
        foreach (var animationAssetId in component.AnimationAssetIds)
        {
            animationsArray.Add(animationAssetId);
        }

        node.Add("animations", animationsArray);
    }

    private static void SaveStaticSpriteComponent(StaticSpriteComponent component, JObject node)
    {
        var spriteData = StaticSpriteDataField?.GetValue(component) as SpriteData;
        node.Add("spriteDataName", spriteData?.Name ?? "null");
        SaveSceneComponent(component, node);
    }

    private static void SaveTileMapComponent(TileMapComponent component, JObject node)
    {
        SaveSceneComponent(component, node);
        node.Add("tile_map_data_asset_id", component.TileMapDataAssetId);
    }

    private static void SaveParticleSystemComponent(ParticleSystemComponent component, JObject node)
    {
        SaveSceneComponent(component, node);
        node.Add("particle_effect_asset_id", component.ParticleEffectAssetId.ToString());
        node.Add("play_on_start", component.PlayOnStart);
        node.Add("looping", component.Looping);
        node.Add("simulate_in_editor", component.SimulateInEditor);
        node.Add("simulation_speed", component.SimulationSpeed);
        node.Add("emission_scale", component.EmissionScale);

        var colorTintNode = new JObject();
        component.ColorTint.Save(colorTintNode);
        node.Add("color_tint", colorTintNode);
    }

    private static void SavePhysicsBaseComponent(PhysicsBaseComponent component, JObject node)
    {
        SaveSceneComponent(component, node);

        var physicsDefinitionNode = new JObject();
        SavePhysicsDefinition(component.PhysicsDefinition, physicsDefinitionNode);
        node.Add("physics_definition", physicsDefinitionNode);
    }

    private static void SaveLightComponent(LightComponent component, JObject node)
    {
        SaveSceneComponent(component, node);

        node.Add("light_type", component.Type.ConvertToString());

        var colorNode = new JObject();
        component.Color.Save(colorNode);
        node.Add("color", colorNode);

        var specularColorNode = new JObject();
        component.SpecularColor.Save(specularColorNode);
        node.Add("specular_color", specularColorNode);

        node.Add("intensity", component.Intensity);
        node.Add("range", component.Range);
        node.Add("inner_cone_angle_degrees", component.InnerConeAngleDegrees);
        node.Add("outer_cone_angle_degrees", component.OuterConeAngleDegrees);
        node.Add("cast_shadows", component.CastShadows);
    }

    private static void SavePrimitiveComponentFlags(PrimitiveComponent component, JObject node)
    {
        node.Add("cast_shadows", component.CastShadows);
        node.Add("receive_shadows", component.ReceiveShadows);
    }

    private static bool ShouldSaveComponent(EntityComponent component)
    {
        return component.GetType().GetCustomAttribute<BrowsableAttribute>()?.Browsable != false;
    }

    private static void SaveCollisionComponent(CollisionComponent component, JObject node)
    {
        SavePhysicsBaseComponent(component, node);
        node.AddArray("fixtures", component.Fixtures, SaveColliderFixture);
    }

    internal static void SaveColliderFixture(ColliderFixture fixture, JObject node)
    {
        var shapeNode = new JObject();
        SaveShape3d(fixture.Shape, shapeNode);
        node.Add("shape", shapeNode);

        var localPositionNode = new JObject();
        fixture.LocalPosition.Save(localPositionNode);
        node.Add("local_position", localPositionNode);

        var localRotationNode = new JObject();
        fixture.LocalRotation.Save(localRotationNode);
        node.Add("local_rotation", localRotationNode);

        node.Add("collision_profile", fixture.ProfileName);
        node.Add("tag", fixture.Tag);
    }

    private static void SaveStaticModelComponent(StaticModelComponent component, JObject node)
    {
        SaveEntityComponent(component, node);

        var localTransformNode = new JObject();
        component.LocalTransform.Save(localTransformNode);
        node.Add("local_transform", localTransformNode);

        var childrenArray = new JArray();
        foreach (var child in component.Children)
        {
            if (child is StaticModelSubMeshComponent { IsGeneratedFromModel: true })
            {
                continue;
            }

            if (!ShouldSaveComponent(child))
            {
                continue;
            }

            var childNode = new JObject();
            SaveComponent(child, childNode);
            childrenArray.Add(childNode);
        }

        node.Add("children_component", childrenArray);
        node.Add("static_model_asset_id", component.StaticModelAssetId.ToString());
        SavePrimitiveComponentFlags(component, node);

        if (component.MaterialOverrides.Count > 0)
        {
            var materialOverridesArray = new JArray();
            foreach (var materialOverride in component.MaterialOverrides)
            {
                if (!materialOverride.HasAnyOverride)
                {
                    continue;
                }

                var overrideNode = new JObject();
                MaterialSlotOverrideJsonSerializer.Save(materialOverride, overrideNode);
                materialOverridesArray.Add(overrideNode);
            }

            if (materialOverridesArray.Count > 0)
            {
                node.Add("material_slot_overrides", materialOverridesArray);
            }
        }
    }

    private static void SaveSkinnedMeshComponent(SkinnedMeshComponent component, JObject node)
    {
        SaveSceneComponent(component, node);
        SavePrimitiveComponentFlags(component, node);
        node.Add("skinned_mesh_id", component.SkinnedMesh?.RiggedModelAssetId ?? Guid.Empty);
    }

    internal static void SavePhysicsDefinition(PhysicsDefinition definition, JObject node)
    {
        node.Add("physics_type", definition.PhysicsType.ConvertToString());
        node.Add("collision_profile", definition.ProfileName);
        node.Add("additional_angular_damping_factor", definition.AdditionalAngularDampingFactor);
        node.Add("additional_angular_damping_threshold_sqr", definition.AdditionalAngularDampingThresholdSqr);
        node.Add("additional_damping", definition.AdditionalDamping);
        node.Add("additional_damping_factor", definition.AdditionalDampingFactor);
        node.Add("additional_linear_damping_threshold_sqr", definition.AdditionalLinearDampingThresholdSqr);
        node.Add("angular_damping", definition.AngularDamping);

        var angularFactorNode = new JObject();
        definition.AngularFactor.Save(angularFactorNode);
        node.Add("angular_factor", angularFactorNode);

        node.Add("angular_sleeping_threshold", definition.AngularSleepingThreshold);
        node.Add("friction", definition.Friction);
        node.Add("linear_damping", definition.LinearDamping);

        var linearFactorNode = new JObject();
        definition.LinearFactor.Save(linearFactorNode);
        node.Add("linear_factor", linearFactorNode);

        node.Add("linear_sleeping_threshold", definition.LinearSleepingThreshold);

        var localInertiaNode = new JObject();
        definition.LocalInertia.Save(localInertiaNode);
        node.Add("local_inertia", localInertiaNode);

        node.Add("mass", definition.Mass);
        node.Add("restitution", definition.Restitution);
        node.Add("rolling_friction", definition.RollingFriction);
        node.Add("apply_gravity", definition.ApplyGravity);

        if (definition.DebugColor is Color debugColor)
        {
            var debugColorNode = new JObject();
            debugColor.Save(debugColorNode);
            node.Add("debug_color", debugColorNode);
        }
        else
        {
            node.Add("debug_color", "null");
        }
    }
}