using System.Reflection;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Serialization;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Framework.Input;

using Newtonsoft.Json.Linq;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.EditorServices;

internal static class EditorAssetJsonSerializer
{
    private static readonly FieldInfo? TextureAssetIdField = typeof(Texture).GetField("_texture2dAssetId", BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool TrySerialize(object asset, out JObject rootObject)
    {
        rootObject = new JObject();

        switch (asset)
        {
            case ButtonsMapping buttonsMapping:
                SaveButtonsMapping(buttonsMapping, rootObject);
                return true;

            case Animation2dData animation2dData:
                SaveAnimation2dData(animation2dData, rootObject);
                return true;

            case Entity entity:
                EditorEntityJsonSerializer.SaveEntity(entity, rootObject);
                return true;

            case World world:
                EditorEntityJsonSerializer.SaveWorld(world, rootObject);
                return true;

            case LitDiffuseMaterial litDiffuseMaterial:
                SaveLitDiffuseMaterial(litDiffuseMaterial, rootObject);
                return true;

            case UnlitTextureMaterial unlitTextureMaterial:
                SaveUnlitTextureMaterial(unlitTextureMaterial, rootObject);
                return true;

            case MaterialAsset materialAsset:
                SaveMaterialAsset(materialAsset, rootObject);
                return true;

            case ParticleEffectAsset particleEffectAsset:
                SaveParticleEffectAsset(particleEffectAsset, rootObject);
                return true;

            case SpriteData spriteData:
                SaveSpriteData(spriteData, rootObject);
                return true;

            case TileMapData tileMapData:
                SaveTileMapData(tileMapData, rootObject);
                return true;

            case TileSetData tileSetData:
                SaveTileSetData(tileSetData, rootObject);
                return true;

            case UIScreenAsset uiScreenAsset:
                SaveUIScreenAsset(uiScreenAsset, rootObject);
                return true;

            case StaticModel staticModel:
                SaveStaticModel(staticModel, rootObject);
                return true;

            case SkeletonAsset skeletonAsset:
                SaveSkeletonAsset(skeletonAsset, rootObject);
                return true;

            case AnimationClipAsset animationClipAsset:
                SaveAnimationClipAsset(animationClipAsset, rootObject);
                return true;

            case SkinnedMeshAsset skinnedMeshAsset:
                SaveSkinnedMeshAsset(skinnedMeshAsset, rootObject);
                return true;

            case Texture texture:
                SaveTexture(texture, rootObject);
                return true;

            default:
                rootObject = null!;
                return false;
        }
    }

    private static void SaveObjectBase(ObjectBase objectBase, JObject node)
    {
        EditorJsonSaveHelper.SaveObjectBase(objectBase, node);
    }

    private static void SaveButtonsMapping(ButtonsMapping buttonsMapping, JObject node)
    {
        SaveObjectBase(buttonsMapping, node);

        var buttonsArrayNode = new JArray();
        foreach (var button in buttonsMapping.Buttons)
        {
            var buttonNode = new JObject();
            SaveInputMapping(button, buttonNode);
            buttonsArrayNode.Add(buttonNode);
        }

        node.Add("buttons", buttonsArrayNode);
    }

    private static void SaveInputMapping(InputMapping inputMapping, JObject node)
    {
        SaveObjectBase(inputMapping, node);

        node.Add("game_pad_number", inputMapping.GamePadNumber.ToString());
        node.Add("button_behavior", inputMapping.ButtonBehavior.ToString());
        node.Add("analog_axis", inputMapping.AnalogAxis.ToString());
        node.Add("invert", inputMapping.Invert);
        node.Add("dead_zone", inputMapping.DeadZone);

        var keyButtonNode = new JObject();
        SaveKeyButton(inputMapping.KeyButton, keyButtonNode);
        node.Add("key_button", keyButtonNode);

        var alternativeKeyButtonNode = new JObject();
        SaveKeyButton(inputMapping.AlternativeKeyButton, alternativeKeyButtonNode);
        node.Add("alternative_key_button", alternativeKeyButtonNode);
    }

    private static void SaveKeyButton(KeyButton keyButton, JObject node)
    {
        node.Add("key", keyButton.Key.ToString());
        node.Add("game_pad_button", keyButton.GamePadButton.ToString());
        node.Add("mouse_button", keyButton.MouseButton.ToString());
        node.Add("input_device", keyButton.InputDevice.ToString());
    }

    private static void SaveAnimationData(AnimationData animationData, JObject node)
    {
        node.Add("animation_type", animationData.AnimationType.ToString());
        SaveObjectBase(animationData, node);
    }

    private static void SaveAnimation2dData(Animation2dData animation2dData, JObject node)
    {
        SaveAnimationData(animation2dData, node);

        var framesArrayNode = new JArray();
        foreach (var frame in animation2dData.Frames)
        {
            var frameNode = new JObject();
            SaveFrameData(frame, frameNode);
            framesArrayNode.Add(frameNode);
        }

        node.Add("frames", framesArrayNode);
    }

    private static void SaveFrameData(FrameData frameData, JObject node)
    {
        node.Add("duration", frameData.Duration);
        node.Add("sprite_id", frameData.SpriteId);
    }

    private static void SaveSpriteData(SpriteData spriteData, JObject node)
    {
        SaveObjectBase(spriteData, node);
        node.Add("sprite_sheet_asset_id", spriteData.SpriteSheetAssetId);

        var locationNode = new JObject();
        spriteData.PositionInTexture.Save(locationNode);
        node.Add("location", locationNode);

        var hotspotNode = new JObject();
        spriteData.Origin.Save(hotspotNode);
        node.Add("hotspot", hotspotNode);

        var collisionsArray = new JArray();
        foreach (var collisionShape in spriteData.CollisionShapes)
        {
            var collisionNode = new JObject();
            EditorEntityJsonSerializer.SaveCollision2d(collisionShape, collisionNode);
            collisionsArray.Add(collisionNode);
        }

        node.Add("collisions", collisionsArray);

        var socketsArray = new JArray();
        foreach (var socket in spriteData.Sockets)
        {
            var socketNode = new JObject();
            SaveSocket(socket, socketNode);
            socketsArray.Add(socketNode);
        }

        node.Add("sockets", socketsArray);
    }

    private static void SaveSocket(Socket socket, JObject node)
    {
        node.Add("name", socket.Name);

        var positionNode = new JObject();
        socket.Position.Save(positionNode);
        node.Add("position", positionNode);
    }

    private static void SaveTileMapData(TileMapData tileMapData, JObject node)
    {
        SaveObjectBase(tileMapData, node);

        var mapSizeNode = new JObject();
        tileMapData.MapSize.Save(mapSizeNode);
        node.Add("map_size", mapSizeNode);
        node.Add("tile_set_asset_id", tileMapData.TileSetDataAssetId);
        if (tileMapData.TileSetDataAssetIds.Count > 1)
        {
            node.Add("tile_set_asset_ids", new JArray(tileMapData.TileSetDataAssetIds));
        }
        SaveCustomProperties(tileMapData.CustomProperties, node);

        var layersArray = new JArray();
        foreach (var layer in tileMapData.Layers)
        {
            var layerNode = new JObject();
            SaveTileMapLayerData(layer, layerNode);
            layersArray.Add(layerNode);
        }

        node.Add("layers", layersArray);

        if (tileMapData.ObjectLayers.Count > 0)
        {
            var objectLayersArray = new JArray();
            foreach (var objectLayer in tileMapData.ObjectLayers)
            {
                var objectLayerNode = new JObject();
                SaveTileMapObjectLayerData(objectLayer, objectLayerNode);
                objectLayersArray.Add(objectLayerNode);
            }

            node.Add("object_layers", objectLayersArray);
        }
    }

    private static void SaveTileSetData(TileSetData tileSetData, JObject node)
    {
        SaveObjectBase(tileSetData, node);
        node.Add("sprite_sheet_asset_id", tileSetData.SpriteSheetAssetId);

        var tileSizeNode = new JObject();
        tileSetData.TileSize.Save(tileSizeNode);
        node.Add("tile_size", tileSizeNode);

        var tilesArray = new JArray();
        foreach (var tile in tileSetData.Tiles)
        {
            var tileNode = new JObject();
            SaveTileData(tile, tileNode);
            tilesArray.Add(tileNode);
        }

        node.Add("tiles", tilesArray);
    }

    private static void SaveUIScreenAsset(UIScreenAsset uiScreenAsset, JObject node)
    {
        SaveObjectBase(uiScreenAsset, node);
        node.Add("source_xaml_file", uiScreenAsset.SourceXamlFile);
        node.Add("theme_name", uiScreenAsset.ThemeName);

        var previewResolutionNode = new JObject();
        uiScreenAsset.PreviewResolution.Save(previewResolutionNode);
        node.Add("preview_resolution", previewResolutionNode);

        node.Add("resource_files", new JArray(uiScreenAsset.ResourceFiles));
    }

    private static void SaveMaterialAsset(MaterialAsset materialAsset, JObject node)
    {
        MaterialAssetJsonSerializer.Save(materialAsset, node);
    }

    private static void SaveParticleEffectAsset(ParticleEffectAsset particleEffectAsset, JObject node)
    {
        ParticleEffectAssetJsonSerializer.Save(particleEffectAsset, node);
    }

    private static void SaveTileMapLayerData(TileMapLayerData layer, JObject node)
    {
        node.Add("name", string.IsNullOrWhiteSpace(layer.Name) ? JValue.CreateNull() : layer.Name);
        node.Add("z_offset", layer.zOffset);
        SaveCustomProperties(layer.CustomProperties, node);
        node.Add("tiles", new JArray(layer.tiles));
        if (layer.HasNonDefaultTileSources())
        {
            var tileSourcesArray = new JArray();
            layer.EnsureTileSourceCount();
            for (var index = 0; index < layer.tileSources.Count; index++)
            {
                tileSourcesArray.Add(layer.tileSources[index]);
            }

            node.Add("tile_sources", tileSourcesArray);
        }

        if (layer.HasTileFlags())
        {
            var flagsArray = new JArray();
            layer.EnsureTileFlagCount();
            for (var index = 0; index < layer.tileFlags.Count; index++)
            {
                flagsArray.Add((int)layer.tileFlags[index]);
            }

            node.Add("tile_flags", flagsArray);
        }
    }

    private static void SaveTileMapObjectLayerData(TileMapObjectLayerData layer, JObject node)
    {
        node.Add("name", string.IsNullOrWhiteSpace(layer.Name) ? JValue.CreateNull() : layer.Name);
        node.Add("z_offset", layer.zOffset);
        SaveCustomProperties(layer.CustomProperties, node);

        var objectsArray = new JArray();
        foreach (var objectData in layer.Objects)
        {
            var objectNode = new JObject();
            SaveTileMapObjectData(objectData, objectNode);
            objectsArray.Add(objectNode);
        }

        node.Add("objects", objectsArray);
    }

    private static void SaveTileMapObjectData(TileMapObjectData objectData, JObject node)
    {
        node.Add("id", objectData.Id);
        node.Add("name", string.IsNullOrWhiteSpace(objectData.Name) ? JValue.CreateNull() : objectData.Name);
        node.Add("type", string.IsNullOrWhiteSpace(objectData.Type) ? JValue.CreateNull() : objectData.Type);
        node.Add("x", objectData.X);
        node.Add("y", objectData.Y);
        node.Add("width", objectData.Width);
        node.Add("height", objectData.Height);
        SaveCustomProperties(objectData.CustomProperties, node);
    }

    private static void SaveStaticModel(StaticModel staticModel, JObject node)
    {
        SaveObjectBase(staticModel, node);

        if (staticModel.RootNode == null)
        {
            node.Add("root_node", JValue.CreateNull());
        }
        else
        {
            var rootNodeObject = new JObject();
            SaveStaticModelNode(staticModel.RootNode, rootNodeObject);
            node.Add("root_node", rootNodeObject);
        }

        node.AddArray("meshes", staticModel.Meshes, SaveStaticModelMesh);
    }

    private static void SaveStaticModelNode(StaticModelNode staticModelNode, JObject node)
    {
        node.Add("name", staticModelNode.Name);
        node.Add("mesh_index", staticModelNode.MeshIndex);

        var positionNode = new JObject();
        staticModelNode.Position.Save(positionNode);
        node.Add("position", positionNode);

        var rotationNode = new JObject();
        staticModelNode.Rotation.Save(rotationNode);
        node.Add("rotation", rotationNode);

        var scaleNode = new JObject();
        staticModelNode.Scale.Save(scaleNode);
        node.Add("scale", scaleNode);

        node.AddArray("children", staticModelNode.Children, SaveStaticModelNode);
    }

    private static void SaveStaticModelMesh(StaticModelMesh staticModelMesh, JObject node)
    {
        node.Add("name", staticModelMesh.Name);
        node.Add("slot_name", staticModelMesh.SlotName);
        node.Add("material_slot_index", staticModelMesh.MaterialSlotIndex);
        node.Add("primitive_type", staticModelMesh.PrimitiveType.ToString());
        node.Add("material_index", staticModelMesh.MaterialIndex);
        node.Add("texture_asset_id", staticModelMesh.TextureAssetId.ToString());

        if (staticModelMesh.MaterialAssetId != Guid.Empty)
        {
            node.Add("material_asset_id", staticModelMesh.MaterialAssetId.ToString());
        }

        if (staticModelMesh.SubMeshes.Count > 0)
        {
            node.AddArray("sub_meshes", staticModelMesh.SubMeshes, SaveSubMesh);
        }

        node.AddArray("vertices", staticModelMesh.GetVertices(), (vertex, vertexNode) => vertex.Save(vertexNode));
        node.AddArray("indices", staticModelMesh.GetIndices());
    }

    private static void SaveSubMesh(SubMesh subMesh, JObject node)
    {
        node.Add("slot_name", subMesh.SlotName);
        node.Add("material_slot_index", subMesh.MaterialSlotIndex);
        node.Add("index_start", subMesh.IndexStart);
        node.Add("primitive_count", subMesh.PrimitiveCount);
        node.Add("vertex_offset", subMesh.VertexOffset);

        if (subMesh.MaterialAssetId != Guid.Empty)
        {
            node.Add("material_asset_id", subMesh.MaterialAssetId.ToString());
        }
    }

    private static void SaveSkeletonAsset(SkeletonAsset skeletonAsset, JObject node)
    {
        SkeletonAssetJsonSerializer.Save(skeletonAsset, node);
    }

    private static void SaveAnimationClipAsset(AnimationClipAsset animationClipAsset, JObject node)
    {
        AnimationClipAssetJsonSerializer.Save(animationClipAsset, node);
    }

    private static void SaveSkinnedMeshAsset(SkinnedMeshAsset skinnedMeshAsset, JObject node)
    {
        SkinnedMeshAssetJsonSerializer.Save(skinnedMeshAsset, node);
    }

    private static void SaveTileData(TileData tile, JObject node)
    {
        node.Add("type", tile.Type.ConvertToString());
        node.Add("id", tile.Id);
        node.Add("collision_type", tile.CollisionType.ConvertToString());
        node.Add("is_breakable", tile.IsBreakable);
        SaveCustomProperties(tile.CustomProperties, node);

        if (tile.CollisionShape == null)
        {
            node.Add("collision", "null");
        }
        else
        {
            var collisionNode = new JObject();
            EditorEntityJsonSerializer.SaveCollision2d(tile.CollisionShape, collisionNode);
            node.Add("collision", collisionNode);
        }

        switch (tile)
        {
            case StaticTileData staticTileData:
                var locationNode = new JObject();
                staticTileData.Location.Save(locationNode);
                node.Add("location", locationNode);
                break;

            case AutoTileData autoTileData:
                node.Add("auto_tile_index", autoTileData.AutoTileIndex);
                var locationsArray = new JArray();
                foreach (var location in autoTileData.Locations)
                {
                    var autoTileLocationNode = new JObject();
                    location.Save(autoTileLocationNode);
                    locationsArray.Add(autoTileLocationNode);
                }

                node.Add("locations", locationsArray);
                break;

            case AnimatedTileData animatedTileData:
                if (!string.IsNullOrWhiteSpace(animatedTileData.Animation2dId))
                {
                    node.Add("animation_2d_id", animatedTileData.Animation2dId);
                }

                if (animatedTileData.Frames.Count > 0)
                {
                    var framesArray = new JArray();
                    for (var frameIndex = 0; frameIndex < animatedTileData.Frames.Count; frameIndex++)
                    {
                        var frame = animatedTileData.Frames[frameIndex];
                        framesArray.Add(new JObject
                        {
                            ["tile_id"] = frame.TileId,
                            ["duration_ms"] = frame.DurationMilliseconds,
                        });
                    }

                    node.Add("animation_frames", framesArray);
                }

                break;
        }
    }

    private static void SaveCustomProperties(Dictionary<string, string> customProperties, JObject node)
    {
        if (customProperties.Count == 0)
        {
            return;
        }

        var propertiesNode = new JObject();
        foreach (var customProperty in customProperties)
        {
            propertiesNode.Add(customProperty.Key, customProperty.Value);
        }

        node.Add("custom_properties", propertiesNode);
    }

    private static void SaveTexture(Texture texture, JObject node)
    {
        SaveObjectBase(texture, node);
        node.Add("texture_asset_id", TextureAssetIdField?.GetValue(texture) as Guid? ?? Guid.Empty);

        var samplerStateNode = new JObject();
        texture.PreferredSamplerState.Save(samplerStateNode);
        node.Add("sampler_state", samplerStateNode);
    }

    private static void SaveMaterialBase(MaterialBase materialBase, JObject node)
    {
        node["id"] = materialBase.Id.ToString();
        node["name"] = materialBase.Name;
        node["is_transparent"] = materialBase.IsTransparent;
        node["queue"] = materialBase.Queue.ToString();
        node["shader_asset_id"] = materialBase.ShaderAssetId.ToString();
        node["cast_shadows"] = materialBase.CastShadows;
        node["receive_shadows"] = materialBase.ReceiveShadows;
        node["blend_state"] = materialBase.GetBlendStateName();
        node["depth_stencil_state"] = materialBase.GetDepthStateName();
        node["rasterizer_state"] = materialBase.GetRasterizerStateName();
        node["sampler_state"] = materialBase.GetSamplerStateName();
    }

    private static void SaveLitDiffuseMaterial(LitDiffuseMaterial material, JObject node)
    {
        SaveMaterialBase(material, node);
        node["type"] = nameof(LitDiffuseMaterial);
        node["BasColor_asset_id"] = material.BasColorAssetId.ToString();
        node["normal_map_asset_id"] = material.NormalMapAssetId.ToString();
        node["diffuse_color"] = new JObject
        {
            ["r"] = material.DiffuseColor.R,
            ["g"] = material.DiffuseColor.G,
            ["b"] = material.DiffuseColor.B,
            ["a"] = material.DiffuseColor.A,
        };
        node["emissive_color"] = new JObject
        {
            ["r"] = material.EmissiveColor.X,
            ["g"] = material.EmissiveColor.Y,
            ["b"] = material.EmissiveColor.Z,
        };
        node["ambient_color"] = new JObject
        {
            ["r"] = material.AmbientColor.X,
            ["g"] = material.AmbientColor.Y,
            ["b"] = material.AmbientColor.Z,
        };
        node["specular_color"] = new JObject
        {
            ["r"] = material.SpecularColor.X,
            ["g"] = material.SpecularColor.Y,
            ["b"] = material.SpecularColor.Z,
        };
        node["specular_power"] = material.SpecularPower;
        node["texture_reflection_asset_id"] = material.ReflectionCubeAssetId.ToString();
    }

    private static void SaveUnlitTextureMaterial(UnlitTextureMaterial material, JObject node)
    {
        SaveMaterialBase(material, node);
        node["type"] = nameof(UnlitTextureMaterial);
        node["BasColor_asset_id"] = material.BasColorAssetId.ToString();
        node["tint_color"] = new JObject
        {
            ["r"] = material.Tint.R,
            ["g"] = material.Tint.G,
            ["b"] = material.Tint.B,
            ["a"] = material.Tint.A,
        };
        node["alpha"] = material.Alpha;
    }
}