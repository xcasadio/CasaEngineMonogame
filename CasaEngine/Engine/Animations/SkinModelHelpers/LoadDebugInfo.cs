using Assimp;
using CasaEngine.Core.Logger;
using Microsoft.VisualBasic.Logging;

// TO DO: 
// For Minimal Info (bottom), we may want to later add more texture type info as we add support for more texture types. 
// (The code in here is a bit crazy to look at, if desired, one could always add some white space - Wil's version is a bit cleaner)

namespace CasaEngine.Engine.Animations.SkinModelHelpers;

public class LoadDebugInfo
{
    private LogManager _logger;

    public LoadDebugInfo()
    {
        _logger = LogManager.Instance;
    }

    public void AssimpSceneDump(SkinModelLoader skinModelLoader, Scene scene)
    {
        if (!skinModelLoader.AssimpInfo)
        {
            return;
        }

        _logger.WriteLineDebug("\n_______________________________________________");
        _logger.WriteLineDebug(" ---------------------");
        _logger.WriteLineDebug(" AssimpSceneDump...");
        _logger.WriteLineDebug(" --------------------- \n ");
        _logger.WriteLineDebug(" Model Name: " + skinModelLoader.FilePathName);
        _logger.WriteLineDebug(" scene.CameraCount: " + scene.CameraCount);
        _logger.WriteLineDebug(" scene.LightCount: " + scene.LightCount);
        _logger.WriteLineDebug(" scene.MeshCount: " + scene.MeshCount);
        _logger.WriteLineDebug(" scene.MaterialCount: " + scene.MaterialCount);
        _logger.WriteLineDebug(" scene.TextureCount: " + scene.TextureCount + "(embedded data)");
        _logger.WriteLineDebug(" scene.AnimationCount: " + scene.AnimationCount);
        _logger.WriteLineDebug(" scene.RootNode.Name: " + scene.RootNode.Name); _logger.WriteLineDebug(" \n ");
        _logger.WriteLineDebug(" ///////////////////////////////////////////////////");
        _logger.WriteLineDebug(" scene.Lights");
        var aiLights = scene.Lights;
        for (int i = 0; i < aiLights.Count; i++)
        {
            var aiLight = aiLights[i];
            _logger.WriteLineDebug(" aiLight " + i + " of " + (aiLights.Count - 1) + "");
            _logger.WriteLineDebug(" aiLight.Name: " + aiLight.Name);
        }
        _logger.WriteLineDebug(" \n ///////////////////////////////////////////////////");
        _logger.WriteLineDebug(" scene.Cameras");
        var aiCameras = scene.Cameras;
        for (int i = 0; i < aiCameras.Count; i++)
        {
            var aiCamera = aiCameras[i];
            _logger.WriteLineDebug(" aiCamera " + i + " of " + (aiCameras.Count - 1) + "");
            _logger.WriteLineDebug(" aiCamera.Name: " + aiCamera.Name);
        }
        _logger.WriteLineDebug(" \n ///////////////////////////////////////////////////");
        _logger.WriteLineDebug(" scene.Meshes");
        var aiMeshes = scene.Meshes;
        for (int i = 0; i < aiMeshes.Count; i++)
        {
            var aiMesh = aiMeshes[i];
            _logger.WriteLineDebug(" \n --------------------------------------------------");
            _logger.WriteLineDebug(" Mesh " + i + " of " + (aiMeshes.Count - 1) + "");
            _logger.WriteLineDebug(" aiMesh.Name: " + aiMesh.Name);
            _logger.WriteLineDebug(" aiMesh.VertexCount: " + aiMesh.VertexCount);
            _logger.WriteLineDebug(" aiMesh.FaceCount: " + aiMesh.FaceCount);
            _logger.WriteLineDebug(" aiMesh.Normals.Count: " + aiMesh.Normals.Count);
            _logger.WriteLineDebug(" aiMesh.MorphMethod: " + aiMesh.MorphMethod);
            _logger.WriteLineDebug(" aiMesh.MaterialIndex: " + aiMesh.MaterialIndex);
            _logger.WriteLineDebug(" aiMesh.MeshAnimationAttachmentCount: " + aiMesh.MeshAnimationAttachmentCount);
            _logger.WriteLineDebug(" aiMesh.Tangents.Count: " + aiMesh.Tangents.Count);
            _logger.WriteLineDebug(" aiMesh.BiTangents.Count: " + aiMesh.BiTangents.Count);
            _logger.WriteLineDebug(" aiMesh.VertexColorChannelCount: " + aiMesh.VertexColorChannelCount);
            _logger.WriteLineDebug(" aiMesh.UVComponentCount.Length: " + aiMesh.UVComponentCount.Length);
            _logger.WriteLineDebug(" aiMesh.TextureCoordinateChannelCount: " + aiMesh.TextureCoordinateChannelCount);
            for (int k = 0; k < aiMesh.TextureCoordinateChannels.Length; k++)
            {
                if (aiMesh.TextureCoordinateChannels[k].Count() > 0)
                {
                    _logger.WriteLineDebug(" aiMesh.TextureCoordinateChannels[" + k + "].Count(): " + aiMesh.TextureCoordinateChannels[k].Count());
                }
            }
            _logger.WriteLineDebug(" aiMesh.BoneCount: " + aiMesh.BoneCount);
            _logger.WriteLineDebug(" \n Bones store a vertex id and a vertex weight. \n ");
            for (int b = 0; b < aiMesh.Bones.Count; b++)
            {
                var aiMeshBone = aiMesh.Bones[b];
                _logger.WriteLineDebug("  aiMesh Bone " + b + " of " + (aiMesh.Bones.Count - 1) + "  aiMeshBone.Name: " + aiMeshBone.Name + "      aiMeshBone.VertexWeightCount: " + aiMeshBone.VertexWeightCount);
                if (aiMeshBone.VertexWeightCount > 0)
                {
                    _logger.WriteLineDebug("    aiMeshBone.VertexWeights[0]VertexID: " + aiMeshBone.VertexWeights[0].VertexID);
                }
            }
        }
        _logger.WriteLineDebug(" \n ///////////////////////////////////////////////////");
        _logger.WriteLineDebug(" scene.Materials");
        var aiMaterials = scene.Materials;
        for (int i = 0; i < aiMaterials.Count; i++)
        {
            var aiMaterial = aiMaterials[i];
            _logger.WriteLineDebug(" \n --------------------------------------------------");
            _logger.WriteLineDebug(" " + "aiMaterial " + i + " of " + (aiMaterials.Count - 1) + "");
            _logger.WriteLineDebug(" " + "aiMaterial.Name: " + aiMaterial.Name);
            _logger.WriteLineDebug(" " + "ColorAmbient: " + aiMaterial.ColorAmbient + "  ColorDiffuse: " + aiMaterial.ColorDiffuse + "  ColorSpecular: " + aiMaterial.ColorSpecular);
            _logger.WriteLineDebug(" " + "ColorEmissive: " + aiMaterial.ColorEmissive + "  ColorReflective: " + aiMaterial.ColorReflective + "  ColorTransparent: " + aiMaterial.ColorTransparent);
            _logger.WriteLineDebug(" " + "Opacity: " + aiMaterial.Opacity + "  Shininess: " + aiMaterial.Shininess + "  ShininessStrength: " + aiMaterial.ShininessStrength);
            _logger.WriteLineDebug(" " + "Reflectivity: " + aiMaterial.Reflectivity + "  ShadingMode: " + aiMaterial.ShadingMode + "  BlendMode: " + aiMaterial.BlendMode + "  BumpScaling: " + aiMaterial.BumpScaling);
            _logger.WriteLineDebug(" " + "IsTwoSided: " + aiMaterial.IsTwoSided + "  IsWireFrameEnabled: " + aiMaterial.IsWireFrameEnabled);
            _logger.WriteLineDebug(" " + "HasTextureAmbient: " + aiMaterial.HasTextureAmbient + "  HasTextureDiffuse: " + aiMaterial.HasTextureDiffuse + "  HasTextureSpecular: " + aiMaterial.HasTextureSpecular);
            _logger.WriteLineDebug(" " + "HasTextureNormal: " + aiMaterial.HasTextureNormal + "  HasTextureDisplacement: " + aiMaterial.HasTextureDisplacement + "  HasTextureHeight: " + aiMaterial.HasTextureHeight + "  HasTextureLightMap: " + aiMaterial.HasTextureLightMap);
            _logger.WriteLineDebug(" " + "HasTextureEmissive: " + aiMaterial.HasTextureEmissive + "  HasTextureOpacity: " + aiMaterial.HasTextureOpacity + "  HasTextureReflection: " + aiMaterial.HasTextureReflection); _logger.WriteLineDebug("");
            // https://github.com/assimp/assimp/issues/3027
            // If the texture data is embedded, the host application can then load 'embedded' texture data directly from the aiScene.mTextures array.
            var aiMaterialTextures = aiMaterial.GetAllMaterialTextures();
            _logger.WriteLineDebug(" aiMaterialTextures.Count: " + aiMaterialTextures.Count());
            for (int j = 0; j < aiMaterialTextures.Count(); j++)
            {
                var aiTexture = aiMaterialTextures[j];
                _logger.WriteLineDebug(" \n   " + "aiMaterialTexture [" + j + "]");
                _logger.WriteLineDebug("   " + "aiTexture.Name: " + skinModelLoader.GetFileName(aiTexture.FilePath, true));
                _logger.WriteLineDebug("   " + "FilePath.: " + aiTexture.FilePath);
                _logger.WriteLineDebug("   " + "texture.TextureType: " + aiTexture.TextureType);
                _logger.WriteLineDebug("   " + "texture.Operation: " + aiTexture.Operation);
                _logger.WriteLineDebug("   " + "texture.BlendFactor: " + aiTexture.BlendFactor);
                _logger.WriteLineDebug("   " + "texture.Mapping: " + aiTexture.Mapping);
                _logger.WriteLineDebug("   " + "texture.WrapModeU: " + aiTexture.WrapModeU + " , V: " + aiTexture.WrapModeV);
                _logger.WriteLineDebug("   " + "texture.UVIndex: " + aiTexture.UVIndex);
            }
        }
        _logger.WriteLineDebug(" \n ///////////////////////////////////////////////////");
        _logger.WriteLineDebug(" scene.Animations \n ");
        var aiAnimations = scene.Animations;
        for (int i = 0; i < aiAnimations.Count; i++)
        {
            var aiAnimation = aiAnimations[i];
            _logger.WriteLineDebug(" --------------------------------------------------");
            _logger.WriteLineDebug(" aiAnimation " + i + " of " + (aiAnimations.Count - 1) + "");
            _logger.WriteLineDebug(" aiAnimation.Name: " + aiAnimation.Name);
            if (aiAnimation.NodeAnimationChannels.Count > 0)
            {
                _logger.WriteLineDebug("  " + " Animated Nodes...");
            }

            var log = "";

            for (int j = 0; j < aiAnimation.NodeAnimationChannels.Count; j++)
            {
                var nodeAnimLists = aiAnimation.NodeAnimationChannels[j];
                _logger.WriteLineDebug("  " + " aiAnimation.NodeAnimationChannels[" + j + "].NodeName: " +
                                       nodeAnimLists.NodeName);
                Node nodeinfo;
                if (GetAssimpTreeNode(scene.RootNode, nodeAnimLists.NodeName, out nodeinfo))
                {
                    if (nodeinfo.MeshIndices.Count > 0)
                    {
                        log += "  " + " HasMeshes: " + nodeinfo.MeshIndices.Count;
                        foreach (var n in nodeinfo.MeshIndices)
                        {
                            log += "  " + " : " + scene.Meshes[n].Name;
                        }
                    }
                }
            }

            _logger.WriteLineDebug(log);
        }
        _logger.WriteLineDebug(" -------------------------------------------------- \n "); _logger.WriteLineDebug(" ///////////////////////////////////////////////////");
        _logger.WriteLineDebug(" scene  NodeHeirarchy");
        AssimpNodeHeirarchyDump(skinModelLoader, scene.RootNode, 0); // ASSIMP NODE HIEARCHY DUMP            
    }

    public void AssimpNodeHeirarchyDump(SkinModelLoader skinModelLoader, Node node, int spaces)
    {
        string indent = "";
        for (int i = 0; i < spaces; i++) indent += "  ";
        _logger.WriteLineDebug("" + indent + "  node.Name: " + node.Name + "          HasMeshes: " + node.HasMeshes + "    MeshCount: " + node.MeshCount +
                      "    node.ChildCount: " + node.ChildCount + "    MeshIndices.Count " + node.MeshIndices.Count);
        for (int j = 0; j < node.MeshIndices.Count; j++)
        {
            var meshIndice = node.MeshIndices[j];
            _logger.WriteLineDebug("" + indent + " *meshIndice: " + meshIndice + "  meshIndice name: " + skinModelLoader._scene.Meshes[meshIndice].Name);
        }
        for (int n = 0; n < node.Children.Count(); n++) { AssimpNodeHeirarchyDump(skinModelLoader, node.Children[n], spaces + 1); }  // recursive
    }

    public bool GetAssimpTreeNode(Node treenode, string name, out Node node)
    {
        bool found = false; node = null;
        if (treenode.Name == name)
        {
            found = true;
            node = treenode;
        }
        else { foreach (var n in treenode.Children) { found = GetAssimpTreeNode(n, name, out node); } }
        return found;
    }

    public void DisplayInfo(SkinModelLoader skinModelLoader, SkinModel model, string filePath)
    {
        if (skinModelLoader.LoadedModelInfo)
        {
            _logger.WriteLineDebug("\n\n\n\n****************************************************");
            _logger.WriteLineDebug("\n@@@DisplayInfo \n \n");
            _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
            _logger.WriteLineDebug("Model");
            _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n");
            _logger.WriteLineDebug("FileName");
            _logger.WriteLineDebug(skinModelLoader.GetFileName(filePath, true));
            _logger.WriteLineDebug("\nPath:");
            _logger.WriteLineDebug(filePath); _logger.WriteLineDebug("");
            _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
            _logger.WriteLineDebug("Animations");
            _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n");
            for (int i = 0; i < skinModelLoader._scene.Animations.Count; i++)
            {
                var anim = skinModelLoader._scene.Animations[i];
                _logger.WriteLineDebug($"_____________________________________");
                _logger.WriteLineDebug($"Anim #[{i}] Name: {anim.Name}");
                _logger.WriteLineDebug($"_____________________________________");
                _logger.WriteLineDebug($"  Duration: {anim.DurationInTicks} / {anim.TicksPerSecond} sec.   total duration in seconds: {anim.DurationInTicks / anim.TicksPerSecond}");
                _logger.WriteLineDebug($"  Node Animation Channels: {anim.NodeAnimationChannelCount} ");
                _logger.WriteLineDebug($"  Mesh Animation Channels: {anim.MeshAnimationChannelCount} ");
                _logger.WriteLineDebug($"  Mesh Morph     Channels: {anim.MeshMorphAnimationChannelCount} ");
            }
            _logger.WriteLineDebug("\n@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
            _logger.WriteLineDebug("Node Hierarchy");
            _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@"); _logger.WriteLineDebug("");
            InfoModelNode(model, model.rootNodeOfTree, 0); _logger.WriteLineDebug("");
            _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
            _logger.WriteLineDebug("Meshes and Materials");
            _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@"); _logger.WriteLineDebug("");
            InfoForMeshMaterials(model, skinModelLoader._scene); _logger.WriteLineDebug("");
        }
        if (skinModelLoader.MinimalInfo || skinModelLoader.LoadedModelInfo)
        {
            MinimalInfo(skinModelLoader, model, filePath);
        }
    }

    public void InfoModelNode(SkinModel model, SkinModel.ModelNode n, int tabLevel)
    {
        string ntab = "";
        for (int i = 0; i < tabLevel; i++) ntab += "  ";
        string rtab = "\n" + ntab;
        string msg = "\n";
        msg += rtab + $"{n.name}  ";
        msg += rtab + $"|_children.Count: {n.children.Count} ";
        if (n.parent == null)
        {
            msg += $"|_parent: IsRoot ";
        }
        else
        {
            msg += $"|_parent: " + n.parent.name;
        }

        msg += rtab + $"|_hasARealBone: {n.hasRealBone} ";
        msg += rtab + $"|_isThisAMeshNode: {n.isMeshNode}";
        if (n.uniqueMeshBones.Count > 0)
        {
            msg += rtab + $"|_uniqueMeshBones.Count: {n.uniqueMeshBones.Count}  ";
            int i = 0;
            foreach (var bone in n.uniqueMeshBones)
            {
                msg += rtab + $"|_node: {n.name}  lists  uniqueMeshBone[{i}] ...  meshIndex: {bone.meshIndex}  meshBoneIndex: {bone.boneIndex}   " + $"mesh[{bone.meshIndex}]bone[{bone.boneIndex}].Name: {model.meshes[bone.meshIndex].meshBones[bone.boneIndex].name}  " + $"in  mesh[{bone.meshIndex}].Name: {model.meshes[bone.meshIndex].Name}";
                var nameToCompare = model.meshes[bone.meshIndex].meshBones[bone.boneIndex].name;
                int j = 0;
                foreach (var anim in model.Animations)
                {
                    int k = 0;
                    foreach (var animNode in anim.animatedNodes)
                    {
                        if (animNode.nodeName == nameToCompare)
                        {
                            msg += rtab + $"|^has corresponding Animation[{j}].Node[{k}].Name: {animNode.nodeName}";
                        }

                        k++;
                    }
                    j++;
                }
                i++;
            }
        }
        _logger.WriteLineDebug(msg);
        for (int i = 0; i < n.children.Count; i++) { InfoModelNode(model, n.children[i], tabLevel + 1); }
    }

    public void InfoForAnimData(SkinModelLoader skinModelLoader, Scene scene)
    {
        if (skinModelLoader.ConsoleInfo)
        {
            string str = "\n\n AssimpSceneConsoleOutput ========= Animation Data========= \n\n";
            _logger.WriteLineDebug(str);
        }
        for (int i = 0; i < scene.Animations.Count; i++)
        {
            var anim = scene.Animations[i];
            if (skinModelLoader.ConsoleInfo)
            {
                _logger.WriteLineDebug($"_________________________________");
                _logger.WriteLineDebug($"Anim #[{i}] Name: {anim.Name}");
                _logger.WriteLineDebug($"_________________________________");
                _logger.WriteLineDebug($"  Duration: {anim.DurationInTicks} / {anim.TicksPerSecond} sec.   total duration in seconds: {anim.DurationInTicks / anim.TicksPerSecond}");
                _logger.WriteLineDebug($"  HasMeshAnimations: {anim.HasMeshAnimations} ");
                _logger.WriteLineDebug($"  Mesh Animation Channels: {anim.MeshAnimationChannelCount} ");
            }
            foreach (var chan in anim.MeshAnimationChannels)
            {
                if (skinModelLoader.ConsoleInfo)
                {
                    _logger.WriteLineDebug($"  Channel MeshName {chan.MeshName}");    // the node name has to be used to tie this channel to the originally printed hierarchy.  BTW, node names must be unique.
                    _logger.WriteLineDebug($"    HasMeshKeys: {chan.HasMeshKeys}");   // access via chan.PositionKeys
                    _logger.WriteLineDebug($"    MeshKeyCount: {chan.MeshKeyCount}"); //                                                               
                }
            }
            if (skinModelLoader.ConsoleInfo)
            {
                _logger.WriteLineDebug($"  Mesh Morph Channels: {anim.MeshMorphAnimationChannelCount} ");
            }

            foreach (var chan in anim.MeshMorphAnimationChannels)
            {
                if (skinModelLoader.ConsoleInfo)
                {
                    _logger.WriteLineDebug($"  Channel {chan.Name}");
                    _logger.WriteLineDebug($"    HasMeshMorphKeys: {chan.HasMeshMorphKeys}");
                    _logger.WriteLineDebug($"     MeshMorphKeyCount: {chan.MeshMorphKeyCount}");
                }
            }
            if (skinModelLoader.ConsoleInfo)
            {
                _logger.WriteLineDebug($"  HasNodeAnimations: {anim.HasNodeAnimations} ");
                _logger.WriteLineDebug($"   Node Channels: {anim.NodeAnimationChannelCount}");
            }
            foreach (var chan in anim.NodeAnimationChannels)
            {
                if (skinModelLoader.ConsoleInfo && skinModelLoader.AnimationKeysInfo)
                {
                    var log = $"   Channel {chan.NodeName}".PadRight(35);                     // the node name has to be used to tie this channel to the originally printed hierarchy.  BTW, node names must be unique.
                    log += $"     Position Keys: {chan.PositionKeyCount}".PadRight(25);    // access via chan.PositionKeys
                    log += $"     Rotation Keys: {chan.RotationKeyCount}".PadRight(25);    // 
                    _logger.WriteLineDebug($"{log}     Scaling  Keys: {chan.ScalingKeyCount}".PadRight(25)); // 
                }
            }
            if (skinModelLoader.ConsoleInfo)
            {
                _logger.WriteLineDebug("\n \n Ok so this is all gonna go into our model class basically as is.");
            }

            foreach (var anode in anim.NodeAnimationChannels)
            {
                if ((skinModelLoader.ConsoleInfo && skinModelLoader.AnimationKeysInfo) || skinModelLoader.targetNodeName == anode.NodeName)
                {
                    _logger.WriteLineDebug($"   Channel {anode.NodeName}\n   (time is in animation ticks it shouldn't exceed anim.DurationInTicks {anim.DurationInTicks} or total duration in seconds: {anim.DurationInTicks / anim.TicksPerSecond})");        // the node name has to be used to tie this channel to the originally printed hierarchy.  node names must be unique.
                    _logger.WriteLineDebug($"     Position Keys: {anode.PositionKeyCount}");       // access via chan.PositionKeys
                    for (int j = 0; j < anode.PositionKeys.Count; j++)
                    {
                        var key = anode.PositionKeys[j];
                        if (skinModelLoader.ConsoleInfo && skinModelLoader.AnimationKeysInfo)
                        {
                            _logger.WriteLineDebug("       index[" + (j + "]").PadRight(5) + " Time: " + key.Time.ToString().PadRight(17) + " secs: "
                                              + (key.Time / anim.TicksPerSecond).ToString() + "  Position: {" + key.Value.ToStringTrimed() + "}");
                        }
                    }
                    if (skinModelLoader.ConsoleInfo && skinModelLoader.AnimationKeysInfo)
                    {
                        _logger.WriteLineDebug($"     Rotation Keys: {anode.RotationKeyCount}");       // 
                    }

                    for (int j = 0; j < anode.RotationKeys.Count; j++)
                    {
                        var key = anode.RotationKeys[j];
                        if (skinModelLoader.ConsoleInfo && skinModelLoader.AnimationKeysInfo)
                        {
                            _logger.WriteLineDebug("       index[" + (j + "]").PadRight(5) + " Time: " + key.Time.ToString() + " secs: "
                                              + (key.Time / anim.TicksPerSecond).ToString() + "  QRotation: {" + key.Value.ToStringTrimed() + "}");
                        }
                    }
                    if (skinModelLoader.ConsoleInfo && skinModelLoader.AnimationKeysInfo)
                    {
                        _logger.WriteLineDebug($"     Scaling  Keys: {anode.ScalingKeyCount}");        // 
                    }

                    for (int j = 0; j < anode.ScalingKeys.Count; j++)
                    {
                        var key = anode.ScalingKeys[j];
                        if (skinModelLoader.ConsoleInfo && skinModelLoader.AnimationKeysInfo)
                        {
                            _logger.WriteLineDebug("       index[" + (j + "]").PadRight(5) + " Time: " + key.Time.ToString() + " secs: "
                                              + (key.Time / anim.TicksPerSecond).ToString() + "  Scaling: {" + key.Value.ToStringTrimed() + "}");
                        }
                    }
                }
            }
        }
    }

    public void InfoForMeshMaterials(SkinModel model, Scene scene)
    {
        _logger.WriteLineDebug("InfoForMaterials");
        _logger.WriteLineDebug("Each mesh has a listing of bones that apply to it; this is just a reference to the bone.");
        _logger.WriteLineDebug("Each mesh has a corresponding Offset matrix for that bone."); _logger.WriteLineDebug("Important.");
        _logger.WriteLineDebug("This means that offsets are not common across meshes but bones can be.");
        _logger.WriteLineDebug("ie: The same bone node may apply to different meshes but that same bone will have a different applicable offset per mesh.");
        _logger.WriteLineDebug("Each mesh also has a corresponding bone weight per mesh.");
        for (int amLoop = 0; amLoop < scene.Meshes.Count; amLoop++)
        {                    // loop through assimp meshes
            Mesh assimpMesh = scene.Meshes[amLoop];
            _logger.WriteLineDebug("\n" + "__________________________" +
                              "\n" + "scene.Meshes[" + amLoop + "] " + assimpMesh.Name +
                              "\n" + " FaceCount: " + assimpMesh.FaceCount +
                              "\n" + " VertexCount: " + assimpMesh.VertexCount +
                              "\n" + " Normals.Count: " + assimpMesh.Normals.Count +
                              "\n" + " Bones.Count: " + assimpMesh.Bones.Count +
                              "\n" + " MaterialIndex: " + assimpMesh.MaterialIndex +
                              "\n" + " MorphMethod: " + assimpMesh.MorphMethod +
                              "\n" + " HasMeshAnimationAttachments: " + assimpMesh.HasMeshAnimationAttachments);
            _logger.WriteLineDebug(" UVComponentCount.Length: " + assimpMesh.UVComponentCount.Length);
            for (int i = 0; i < assimpMesh.UVComponentCount.Length; i++)
            {
                int val = assimpMesh.UVComponentCount[i];
                if (val > 0)
                {
                    _logger.WriteLineDebug("   mesh.UVComponentCount[" + i + "] : int value: " + val);
                }
            }
            _logger.WriteLineDebug(" TextureCoordinateChannels.Length:" + assimpMesh.TextureCoordinateChannels.Length);
            _logger.WriteLineDebug(" TextureCoordinateChannelCount:" + assimpMesh.TextureCoordinateChannelCount);
            for (int i = 0; i < assimpMesh.TextureCoordinateChannels.Length; i++)
            {
                var channel = assimpMesh.TextureCoordinateChannels[i];
                if (channel.Count > 0)
                {
                    _logger.WriteLineDebug("   mesh.TextureCoordinateChannels[" + i + "]  count " + channel.Count);
                }
                //for (int j = 0; j < channel.Count; j++) { // holds uvs and ? i think //_logger.WriteLineDebug(" channel[" + j + "].Count: " + channel.Count); }
            }
            _logger.WriteLineDebug("");
        }
        _logger.WriteLineDebug("\n" + "__________________________");
        if (scene.HasTextures)
        {
            var texturescount = scene.TextureCount;
            var textures = scene.Textures;
            _logger.WriteLineDebug("\n  Embedded Textures " + " Count " + texturescount);
            for (int i = 0; i < textures.Count; i++)
            {
                var name = textures[i];
                _logger.WriteLineDebug("    Embedded Textures[" + i + "] " + name);
            }
        }
        else { _logger.WriteLineDebug("\n    Embedded Textures " + " None "); }
        _logger.WriteLineDebug("\n" + "__________________________");
        if (scene.HasMaterials)
        {
            _logger.WriteLineDebug("\n    Materials scene.MaterialCount " + scene.MaterialCount + "\n");
            for (int i = 0; i < scene.Materials.Count; i++)
            {
                _logger.WriteLineDebug("");
                _logger.WriteLineDebug("\n    " + "__________________________");
                _logger.WriteLineDebug("    Material[" + i + "] ");
                _logger.WriteLineDebug("    Material[" + i + "].Name " + scene.Materials[i].Name);
                var m = scene.Materials[i];
                var t = m.GetAllMaterialTextures();
                _logger.WriteLineDebug($"{(m.HasName ? "     Name: " + m.Name : string.Empty)}    GetAllMaterialTextures Length " + t.Length);
                _logger.WriteLineDebug("");
                for (int j = 0; j < t.Length; j++)
                {
                    var tindex = t[j].TextureIndex;
                    var toperation = t[j].Operation;
                    var ttype = t[j].TextureType.ToString();
                    var tfilepath = t[j].FilePath;
                    // J matches up to the texture coordinate channel uv count it looks like.
                    _logger.WriteLineDebug("    Texture[" + j + "] " + "   Index:" + tindex + "   Type: " + ttype + "   Operation: " + toperation + "   Filepath: " + tfilepath);
                }
                _logger.WriteLineDebug("");
                // added info
                _logger.WriteLineDebug("    Material[" + i + "] " + "  HasColorAmbient " + m.HasColorAmbient + "  HasColorDiffuse " + m.HasColorDiffuse + "  HasColorSpecular " + m.HasColorSpecular);
                _logger.WriteLineDebug("    Material[" + i + "] " + "  HasColorReflective " + m.HasColorReflective + "  HasColorEmissive " + m.HasColorEmissive + "  HasColorTransparent " + m.HasColorTransparent);
                _logger.WriteLineDebug("    Material[" + i + "] " + "  ColorAmbient:" + m.ColorAmbient + "  ColorDiffuse: " + m.ColorDiffuse + "  ColorSpecular: " + m.ColorSpecular);
                _logger.WriteLineDebug("    Material[" + i + "] " + "  ColorReflective:" + m.ColorReflective + "  ColorEmissive: " + m.ColorEmissive + "  ColorTransparent: " + m.ColorTransparent);
                _logger.WriteLineDebug("    Material[" + i + "] " + "  HasOpacity: " + m.HasOpacity + "  Opacity: " + m.Opacity + "  HasShininess:" + m.HasShininess + "  Shininess:" + m.Shininess + "  HasReflectivity: " + m.HasReflectivity + "  Reflectivity " + scene.Materials[i].Reflectivity);
                _logger.WriteLineDebug("    Material[" + i + "] " + "  HasBlendMode:" + m.HasBlendMode + "  BlendMode:" + m.BlendMode + "  HasShadingMode: " + m.HasShadingMode + "  ShadingMode:" + m.ShadingMode + "  HasBumpScaling: " + m.HasBumpScaling + "  HasTwoSided: " + m.HasTwoSided + "  IsTwoSided: " + m.IsTwoSided);
                _logger.WriteLineDebug("    Material[" + i + "] " + "  HasTextureAmbient " + m.HasTextureAmbient + "  HasTextureDiffuse " + m.HasTextureDiffuse + "  HasTextureSpecular " + m.HasTextureSpecular);
                _logger.WriteLineDebug("    Material[" + i + "] " + "  HasTextureNormal " + m.HasTextureNormal + "  HasTextureHeight " + m.HasTextureHeight + "  HasTextureDisplacement:" + m.HasTextureDisplacement + "  HasTextureLightMap " + m.HasTextureLightMap);
                _logger.WriteLineDebug("    Material[" + i + "] " + "  HasTextureReflection:" + m.HasTextureReflection + "  HasTextureOpacity " + m.HasTextureOpacity + "  HasTextureEmissive:" + m.HasTextureEmissive);
            }
            _logger.WriteLineDebug("");
        }
        else
        {
            _logger.WriteLineDebug("\n   No Materials Present. \n");
        }
        _logger.WriteLineDebug("");
        _logger.WriteLineDebug("\n" + "__________________________");
        _logger.WriteLineDebug("Bones in meshes");
        for (int mindex = 0; mindex < model.meshes.Length; mindex++)
        {
            var rmMesh = model.meshes[mindex];
            _logger.WriteLineDebug(""); _logger.WriteLineDebug("\n" + "__________________________");
            _logger.WriteLineDebug("Bones in mesh[" + mindex + "]   " + rmMesh.Name); _logger.WriteLineDebug("");
            if (rmMesh.hasBones)
            {
                var meshBones = rmMesh.meshBones;
                _logger.WriteLineDebug(" meshBones.Length: " + meshBones.Length);
                for (int meshBoneIndex = 0; meshBoneIndex < meshBones.Length; meshBoneIndex++)
                {
                    var boneInMesh = meshBones[meshBoneIndex]; // ahhhh
                    var boneInMeshName = meshBones[meshBoneIndex].name;
                    string str = "   mesh[" + mindex + "].Name: " + rmMesh.Name + "   bone[" + meshBoneIndex + "].Name: " + boneInMeshName + "   assimpMeshBoneIndex: " + meshBoneIndex.ToString() + "   WeightCount: " + boneInMesh.numWeightedVerts; //str += "\n" + "   OffsetMatrix " + boneInMesh.OffsetMatrix;
                    _logger.WriteLineDebug(str);
                }
            }
            _logger.WriteLineDebug("");
        }
    }

    public void MinimalInfo(SkinModelLoader skinModelLoader, SkinModel model, string filePath)
    {
        _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
        _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@"); _logger.WriteLineDebug("");
        _logger.WriteLineDebug($"Model");
        _logger.WriteLineDebug($"{skinModelLoader.GetFileName(filePath, true)}  Loaded"); _logger.WriteLineDebug("");
        _logger.WriteLineDebug("Model sceneRootNodeOfTree's Node Name:     " + model.rootNodeOfTree.name);
        _logger.WriteLineDebug("Model number of animaton: " + model.Animations.Count);
        _logger.WriteLineDebug("Model number of meshes:   " + model.meshes.Length);
        for (int mmLoop = 0; mmLoop < model.meshes.Length; mmLoop++)
        {
            var rmMesh = model.meshes[mmLoop];
            _logger.WriteLineDebug("Model mesh #" + mmLoop + " of  " + model.meshes.Length + "   Name: " + rmMesh.Name + "   MaterialIndex: " + rmMesh.material_index + "  MaterialIndexName: " + rmMesh.material_name + "  Bones.Count " + model.meshes[mmLoop].meshBones.Count() + " ([0] is a generated bone to the mesh)");
            if (rmMesh.tex_diffuse != null)
            {
                _logger.WriteLineDebug("texture: " + rmMesh.tex_name);
            }

            if (rmMesh.tex_normalMap != null)
            {
                _logger.WriteLineDebug("textureNormalMap: " + rmMesh.tex_normMap_name);
            }
            /// May add more texture types later in which case we may want to update this for debugging if needed
            //if (rmMesh.textureHeightMap != null) _logger.WriteLineDebug("textureHeightMap: " + rmMesh.textureHeightMapName);
        }
        _logger.WriteLineDebug(""); _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
        _logger.WriteLineDebug("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@"); _logger.WriteLineDebug("\n");
    }

    public void CreatingRootInfo(string str1)
    {
        _logger.WriteLineDebug("\n\n@@@CreateRootNode \n");
        _logger.WriteLineDebug("\n\n Prep to build a models tree. Set Up the Models RootNode");
        _logger.WriteLineDebug(str1);
    }

    public void ShowMeshBoneCreationInfo(Mesh assimpMesh, SkinModel.SkinMesh sMesh, bool MatrixInfo, int mi)
    {
        // If an imported model uses multiple materials, the import splits up the mesh. Use this value as index into the scene's material list. 
        // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e                   
        _logger.WriteLineDebug("\n Name " + assimpMesh.Name + " scene.Mesh[" + mi + "] ");
        _logger.WriteLineDebug("" + " assimpMesh.VertexCount: " + assimpMesh.VertexCount + "  rmMesh.MaterialIndexName: " + sMesh.material_name
                      + "   Material index: " + sMesh.material_index + " (material associated to this mesh)  " + " Bones.Count: " + assimpMesh.Bones.Count);
        _logger.WriteLineDebug("" + " Note bone 0 doesn't exist in the original assimp bone data structure to facilitate a bone 0 for mesh node transforms so " +
                      "that aibone[0] is converted to modelBone[1]");
        for (int i = 0; i < sMesh.meshBones.Length; i++)
        {
            var bone = sMesh.meshBones[i];
            _logger.WriteLineDebug(" Bone [" + i + "] Name " + bone.name + "  meshIndex: " + bone.meshIndex + " meshBoneIndex: "
                          + bone.boneIndex + " numberOfAssociatedWeightedVertices: " + bone.numWeightedVerts);
            if (MatrixInfo)
            {
                _logger.WriteLineDebug("  Offset: " + bone.offset_mtx);
            }
        }
    }

    public void ShowNodeTreeInfo(int tabLevel, Node curAssimpNode, bool matrixInfo, SkinModel.ModelNode modelNode, SkinModel model, Scene scene)
    {
        string ntab = "";
        for (int i = 0; i < tabLevel; i++) ntab += "  ";
        string ntab2 = ntab + "    ";

        _logger.WriteLineDebug("\n\n@@@CreateModelNodeTreeTransformsRecursively \n \n ");
        _logger.WriteLineDebug(" " + ntab + "  ModelNode Name: " + modelNode.name + "  curAssimpNode.Name: " + curAssimpNode.Name);
        if (curAssimpNode.MeshIndices.Count > 0)
        {
            _logger.WriteLineDebug(" " + ntab + "  |_This node has mesh references.  aiMeshCount: " + curAssimpNode.MeshCount + $" Listed MeshIndices: {string.Join(", ", curAssimpNode.MeshIndices)}");
            for (int i = 0; i < curAssimpNode.MeshIndices.Count; i++)
            {
                var nodesmesh = model.meshes[curAssimpNode.MeshIndices[i]];
                _logger.WriteLineDebug(" " + ntab + " " + " |_Is a mesh ... Mesh nodeRefContainingAnimatedTransform Set to node: "
                              + nodesmesh.node_with_anim_trans.name + "  mesh: " + nodesmesh.Name);
            }
        }
        if (matrixInfo)
        {
            _logger.WriteLineDebug("\n " + ntab2 + "|_curAssimpNode.Transform: " + curAssimpNode.Transform.SrtInfoToString(ntab2));
        }

        for (int mIndex = 0; mIndex < scene.Meshes.Count; mIndex++)
        {
            SkinModel.ModelBone bone;
            int boneIndexInMesh = 0;
            if (GetBoneForMesh(model.meshes[mIndex], modelNode.name, out bone, out boneIndexInMesh))
            {
                var adjustedBoneIndexInMesh = boneIndexInMesh;
                _logger.WriteLineDebug(" " + ntab + "  |_The node will be marked as having a real bone node along the bone route.");
                if (modelNode.isMeshNode)
                {
                    _logger.WriteLineDebug(" " + ntab + "  |_The node is also a mesh node so this is maybe a node targeting a mesh transform with animations.");
                }

                _logger.WriteLineDebug(" " + ntab + "  |_Adding uniqueBone for Node: " + modelNode.name + " of Mesh[" + mIndex + " of " + scene.Meshes.Count + "].Name: " + scene.Meshes[mIndex].Name);
                _logger.WriteLineDebug(" " + ntab + "  |_It's a Bone  in mesh #" + mIndex + "  aiBoneIndexInMesh: " + (boneIndexInMesh - 1) + " adjusted BoneIndexInMesh: " + adjustedBoneIndexInMesh);
            }
        }
    }
    private bool GetBoneForMesh(SkinModel.SkinMesh sMesh, string name, out SkinModel.ModelBone bone, out int boneIndexInMesh)
    {
        bool found = false; bone = null; boneIndexInMesh = 0;
        for (int j = 0; j < sMesh.meshBones.Length; j++)
        {  // loop thru the bones of the mesh
            if (sMesh.meshBones[j].name == name)
            {          // found a bone whose name matches 
                found = true;
                bone = sMesh.meshBones[j];                  // return the matching bone
                boneIndexInMesh = j;                        // return the index into the bone-list of the mesh
            }
        }
        return found;
    }

    public void ShowMeshInfo(Mesh mesh, int mi)
    {
        _logger.WriteLineDebug(
            Environment.NewLine + "__________________________" +
            Environment.NewLine + "scene.Meshes[" + mi + "] " + mesh.Name +
            Environment.NewLine + " FaceCount: " + mesh.FaceCount +
            Environment.NewLine + " VertexCount: " + mesh.VertexCount +
            Environment.NewLine + " Normals.Count: " + mesh.Normals.Count +
            Environment.NewLine + " Bones.Count: " + mesh.Bones.Count +
            Environment.NewLine + " HasMeshAnimationAttachments: " + mesh.HasMeshAnimationAttachments + "\n  (note mesh animations maybe linked to a node animation off the main bone transform chain.)" +
            Environment.NewLine + " MorphMethod: " + mesh.MorphMethod +
            Environment.NewLine + " MaterialIndex: " + mesh.MaterialIndex +
            Environment.NewLine + " VertexColorChannels.Count: " + mesh.VertexColorChannels[mi].Count +
            Environment.NewLine + " Tangents.Count: " + mesh.Tangents.Count +
            Environment.NewLine + " BiTangents.Count: " + mesh.BiTangents.Count +
            Environment.NewLine + " UVComponentCount.Length: " + mesh.UVComponentCount.Length
        );
    }
}