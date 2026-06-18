using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GltfAnimation = SharpGLTF.Schema2.Animation;
using GltfMaterial = SharpGLTF.Schema2.Material;
using GltfMesh = SharpGLTF.Schema2.Mesh;
using GltfModelRoot = SharpGLTF.Schema2.ModelRoot;
using GltfNode = SharpGLTF.Schema2.Node;
using GltfPrimitive = SharpGLTF.Schema2.MeshPrimitive;
using GltfSkin = SharpGLTF.Schema2.Skin;
using Quaternion = Microsoft.Xna.Framework.Quaternion;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;
using Vector4 = Microsoft.Xna.Framework.Vector4;

namespace CasaEngine.Framework.Assets.Animations;

/// <summary>
/// Reads a glTF/GLB file into a <see cref="RiggedModel"/> using SharpGLTF.
/// Populates the same raw structures the legacy Assimp <see cref="RiggedModelLoader"/>
/// produces (node tree, flat node/bone lists, inverse-bind offset matrices, skinned
/// mesh vertices, and <see cref="RiggedModel.OriginalAnimations"/>), then calls
/// <see cref="RiggedModel.InitializeRuntimeAnimation"/> so the existing skeleton/clip
/// construction is reused unchanged.
/// <para/>
/// Conventions: SharpGLTF exposes <see cref="System.Numerics.Matrix4x4"/> in the same
/// row-vector layout as XNA, so matrices are copied field-for-field with NO transpose
/// (unlike the Assimp path which used <c>ToMonoGameTransposed()</c>). Triangle winding
/// is reversed once to match the legacy <c>FlipWindingOrder</c>; UVs are not flipped.
/// Non-glTF formats must be converted to glTF first (editor side, AssimpNetter).
/// </summary>
public sealed class GltfRiggedModelReader
{
    private readonly AssetContentManager _assetContentManager;
    private readonly Effect _effectToUse;
    private const int DefaultAnimatedFramesPerSecondLod = 24;
    private const float AddedLoopingDuration = 0.5f;

    public GltfRiggedModelReader(AssetContentManager content = null, Effect defaultEffect = null)
    {
        _assetContentManager = content;
        _effectToUse = defaultEffect;
    }

    public bool IsFileSupported(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        return extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".glb", StringComparison.OrdinalIgnoreCase);
    }

    public RiggedModel LoadAsset(string filePath, AssetContentManager content)
    {
        return LoadAssetInternal(filePath, content ?? _assetContentManager);
    }

    public RiggedModel LoadAsset(string filePath)
    {
        return LoadAssetInternal(filePath, _assetContentManager);
    }

    private RiggedModel LoadAssetInternal(string filePath, AssetContentManager content)
    {
        filePath = ResolveModelPath(filePath);

        GltfModelRoot root;
        try
        {
            root = GltfModelRoot.Load(filePath, new SharpGLTF.Schema2.ReadSettings
            {
                Validation = SharpGLTF.Validation.ValidationMode.Skip
            });
        }
        catch (Exception ex)
        {
            Logs.WriteException(ex);
            return null;
        }

        var model = new RiggedModel();
        if (_effectToUse != null)
        {
            model.Effect = _effectToUse;
        }

        // Collect every skin joint and its inverse-bind matrix (first occurrence wins).
        var inverseBindByJoint = new Dictionary<GltfNode, Matrix>();
        foreach (var skin in root.LogicalSkins)
        {
            for (int jointIndex = 0; jointIndex < skin.JointsCount; jointIndex++)
            {
                var (joint, inverseBind) = skin.GetJoint(jointIndex);
                if (joint != null && !inverseBindByJoint.ContainsKey(joint))
                {
                    inverseBindByJoint[joint] = ToXna(inverseBind);
                }
            }
        }

        // Bone palette index 0 is a dummy, exactly like the Assimp loader.
        CreateDummyFlatListNodeZero(model);

        var nodeMap = new Dictionary<GltfNode, RiggedModel.RiggedModelNode>();
        var meshNodes = new List<(GltfNode GltfNode, RiggedModel.RiggedModelNode ModelNode)>();

        var scene = root.DefaultScene ?? (root.LogicalScenes.Count > 0 ? root.LogicalScenes[0] : null);
        var topLevelNodes = scene != null ? scene.VisualChildren.ToList() : new List<GltfNode>();

        if (topLevelNodes.Count == 1)
        {
            // Use the single top-level node as the model root directly (Assimp wraps scenes in one
            // "RootNode"); adding a synthetic wrapper would duplicate that node's name.
            var rootNode = new RiggedModel.RiggedModelNode { IsTheRootNode = true };
            model.RootNodeOfTree = rootNode;
            BuildNodeRecursive(model, rootNode, topLevelNodes[0], inverseBindByJoint, nodeMap, meshNodes);
        }
        else
        {
            // Multiple top-level nodes: wrap them under a synthetic, uniquely-named root.
            var rootNode = new RiggedModel.RiggedModelNode
            {
                Name = "__GltfRoot__",
                IsTheRootNode = true,
            };
            model.RootNodeOfTree = rootNode;
            model.FlatListToAllNodes.Add(rootNode);
            model.NumberOfNodesInUse++;

            foreach (var gltfNode in topLevelNodes)
            {
                var childNode = new RiggedModel.RiggedModelNode { Parent = rootNode };
                rootNode.Children.Add(childNode);
                BuildNodeRecursive(model, childNode, gltfNode, inverseBindByJoint, nodeMap, meshNodes);
            }
        }

        // Meshes are built after the whole tree so every bone already owns a palette index.
        var meshList = new List<RiggedModel.RiggedModelMesh>();
        foreach (var (gltfNode, modelNode) in meshNodes)
        {
            BuildMeshesForNode(gltfNode, modelNode, nodeMap, meshList, content);
        }
        model.Meshes = meshList.ToArray();

        BuildOriginalAnimations(model, root, nodeMap);

        model.InitializeRuntimeAnimation();
        return model;
    }

    private static void CreateDummyFlatListNodeZero(RiggedModel model)
    {
        var dummy = new RiggedModel.RiggedModelNode
        {
            Name = "DummyBone0",
            BoneShaderFinalTransformIndex = model.FlatListToBoneNodes.Count,
        };
        model.FlatListToBoneNodes.Add(dummy);
        model.NumberOfBonesInUse++;
    }

    private void BuildNodeRecursive(
        RiggedModel model,
        RiggedModel.RiggedModelNode modelNode,
        GltfNode gltfNode,
        IReadOnlyDictionary<GltfNode, Matrix> inverseBindByJoint,
        Dictionary<GltfNode, RiggedModel.RiggedModelNode> nodeMap,
        List<(GltfNode, RiggedModel.RiggedModelNode)> meshNodes)
    {
        modelNode.Name = gltfNode.Name ?? string.Empty;

        var localMatrix = ToXna(gltfNode.LocalMatrix);
        modelNode.BindLocalTransformMg = localMatrix;
        modelNode.LocalTransformMg = localMatrix;
        nodeMap[gltfNode] = modelNode;

        if (inverseBindByJoint.TryGetValue(gltfNode, out var inverseBind))
        {
            modelNode.IsThisARealBone = true;
            modelNode.IsANodeAlongTheBoneRoute = true;
            modelNode.OffsetMatrixMg = inverseBind;
            MarkParentsNecessary(modelNode);
            modelNode.BoneShaderFinalTransformIndex = model.FlatListToBoneNodes.Count;
            model.FlatListToBoneNodes.Add(modelNode);
            model.NumberOfBonesInUse++;
        }

        if (gltfNode.Mesh != null && gltfNode.Mesh.Primitives.Count > 0)
        {
            modelNode.IsThisAMeshNode = true;
            modelNode.InvOffsetMatrixMg = Matrix.Invert(modelNode.LocalTransformMg);
            meshNodes.Add((gltfNode, modelNode));
        }

        model.FlatListToAllNodes.Add(modelNode);
        model.NumberOfNodesInUse++;

        foreach (var childGltfNode in gltfNode.VisualChildren)
        {
            var childNode = new RiggedModel.RiggedModelNode { Parent = modelNode };
            if (modelNode.IsANodeAlongTheBoneRoute)
            {
                childNode.IsANodeAlongTheBoneRoute = true;
            }

            modelNode.Children.Add(childNode);
            BuildNodeRecursive(model, childNode, childGltfNode, inverseBindByJoint, nodeMap, meshNodes);
        }
    }

    private static void MarkParentsNecessary(RiggedModel.RiggedModelNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            parent.IsThisNodeTransformNecessary = true;
            parent.IsANodeAlongTheBoneRoute = true;
            parent = parent.Parent;
        }
    }

    // ------------------------------------------------------------------
    //  Geometry + skinning
    // ------------------------------------------------------------------

    private void BuildMeshesForNode(
        GltfNode gltfNode,
        RiggedModel.RiggedModelNode modelNode,
        IReadOnlyDictionary<GltfNode, RiggedModel.RiggedModelNode> nodeMap,
        List<RiggedModel.RiggedModelMesh> meshList,
        AssetContentManager content)
    {
        int[] jointPalette = BuildJointPalette(gltfNode.Skin, nodeMap);

        foreach (var primitive in gltfNode.Mesh.Primitives)
        {
            var mesh = BuildMesh(gltfNode.Mesh.Name, primitive, jointPalette, content);
            mesh.NodeRefContainingAnimatedTransform = modelNode;
            mesh.MeshInitialTransformFromNodeMg = modelNode.LocalTransformMg;
            mesh.MeshCombinedFinalTransformMg = Matrix.Identity;
            meshList.Add(mesh);
        }
    }

    private static int[] BuildJointPalette(GltfSkin skin, IReadOnlyDictionary<GltfNode, RiggedModel.RiggedModelNode> nodeMap)
    {
        if (skin == null)
        {
            return Array.Empty<int>();
        }

        var palette = new int[skin.JointsCount];
        for (int jointIndex = 0; jointIndex < skin.JointsCount; jointIndex++)
        {
            var (joint, _) = skin.GetJoint(jointIndex);
            palette[jointIndex] = joint != null && nodeMap.TryGetValue(joint, out var node)
                ? node.BoneShaderFinalTransformIndex
                : 0;
        }

        return palette;
    }

    private RiggedModel.RiggedModelMesh BuildMesh(string gltfMeshName, GltfPrimitive primitive, int[] jointPalette, AssetContentManager content)
    {
        var mesh = new RiggedModel.RiggedModelMesh
        {
            NameOfMesh = gltfMeshName ?? string.Empty,
            MaterialIndex = primitive.Material?.LogicalIndex ?? -1,
        };

        var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
        int vertexCount = positions?.Count ?? 0;
        var vertices = new VertexPositionTextureNormalTangentWeights[vertexCount];

        var normals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
        var uvs = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
        var tangents = primitive.GetVertexAccessor("TANGENT")?.AsVector4Array();
        var colors = primitive.GetVertexAccessor("COLOR_0")?.AsVector4Array();
        var joints = primitive.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
        var weights = primitive.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();

        bool hasColors = colors != null && colors.Count == vertexCount;
        mesh.HasVertexColors = hasColors;

        for (int k = 0; k < vertexCount; k++)
        {
            var p = positions![k];
            vertices[k].Position = new Vector3(p.X, p.Y, p.Z);

            if (normals != null && k < normals.Count)
            {
                var n = normals[k];
                vertices[k].Normal = new Vector3(n.X, n.Y, n.Z);
            }

            if (uvs != null && k < uvs.Count)
            {
                var uv = uvs[k];
                vertices[k].TextureCoordinate = new Vector2(uv.X, uv.Y);
            }

            if (tangents != null && k < tangents.Count)
            {
                var t = tangents[k];
                vertices[k].Tangent = new Vector3(t.X, t.Y, t.Z);
            }

            vertices[k].Color = hasColors
                ? new Vector4(colors![k].X, colors[k].Y, colors[k].Z, colors[k].W)
                : new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

            AssignVertexSkinning(ref vertices[k], joints, weights, jointPalette, k);
        }

        BuildBounds(mesh, vertices);

        // Normalize to a triangle list, reversing winding (legacy FlipWindingOrder parity).
        var indexList = new List<int>(vertexCount);
        foreach (var triangle in primitive.GetTriangleIndices())
        {
            indexList.Add(triangle.A);
            indexList.Add(triangle.C);
            indexList.Add(triangle.B);
        }

        mesh.Vertices = vertices;
        mesh.Indices = indexList.ToArray();

        LoadBaseColorTexture(mesh, primitive.Material, content);
        return mesh;
    }

    private static void AssignVertexSkinning(
        ref VertexPositionTextureNormalTangentWeights vertex,
        IList<System.Numerics.Vector4> joints,
        IList<System.Numerics.Vector4> weights,
        int[] jointPalette,
        int vertexIndex)
    {
        if (joints == null || weights == null || jointPalette.Length == 0
            || vertexIndex >= joints.Count || vertexIndex >= weights.Count)
        {
            // No skin: bind fully to the dummy bone 0 (identity), matching the Assimp loader.
            vertex.BlendIndices = new Vector4(0f, 0f, 0f, 0f);
            vertex.BlendWeights = new Vector4(1f, 0f, 0f, 0f);
            return;
        }

        var localJoints = joints[vertexIndex];
        var jointWeights = weights[vertexIndex];

        vertex.BlendIndices = new Vector4(
            MapJoint(jointPalette, localJoints.X),
            MapJoint(jointPalette, localJoints.Y),
            MapJoint(jointPalette, localJoints.Z),
            MapJoint(jointPalette, localJoints.W));
        vertex.BlendWeights = new Vector4(jointWeights.X, jointWeights.Y, jointWeights.Z, jointWeights.W);
    }

    private static float MapJoint(int[] jointPalette, float localJointIndex)
    {
        int index = (int)localJointIndex;
        return index >= 0 && index < jointPalette.Length ? jointPalette[index] : 0;
    }

    private static void BuildBounds(RiggedModel.RiggedModelMesh mesh, VertexPositionTextureNormalTangentWeights[] vertices)
    {
        var min = Vector3.Zero;
        var max = Vector3.Zero;
        var centroid = Vector3.Zero;

        foreach (var vertex in vertices)
        {
            if (vertex.Position.X < min.X) { min.X = vertex.Position.X; }
            if (vertex.Position.Y < min.Y) { min.Y = vertex.Position.Y; }
            if (vertex.Position.Z < min.Z) { min.Z = vertex.Position.Z; }
            if (vertex.Position.X > max.X) { max.X = vertex.Position.X; }
            if (vertex.Position.Y > max.Y) { max.Y = vertex.Position.Y; }
            if (vertex.Position.Z > max.Z) { max.Z = vertex.Position.Z; }
            centroid += vertex.Position;
        }

        mesh.Min = min;
        mesh.Max = max;
        mesh.Centroid = vertices.Length > 0 ? centroid / vertices.Length : Vector3.Zero;
    }

    private void LoadBaseColorTexture(RiggedModel.RiggedModelMesh mesh, GltfMaterial material, AssetContentManager content)
    {
        var graphicsDevice = content?.GraphicsDevice;
        if (graphicsDevice == null || material == null)
        {
            return;
        }

        var image = material.FindChannel("BaseColor")?.Texture?.PrimaryImage;
        if (image == null || image.Content.IsEmpty)
        {
            return;
        }

        try
        {
            using var stream = new MemoryStream(image.Content.Content.ToArray());
            mesh.Texture = Texture2D.FromStream(graphicsDevice, stream);
            mesh.TextureName = string.IsNullOrWhiteSpace(material.Name) ? mesh.NameOfMesh : material.Name;
        }
        catch (Exception ex)
        {
            Logs.WriteException(ex);
        }
    }

    // ------------------------------------------------------------------
    //  Animations
    // ------------------------------------------------------------------

    private void BuildOriginalAnimations(
        RiggedModel model,
        GltfModelRoot root,
        IReadOnlyDictionary<GltfNode, RiggedModel.RiggedModelNode> nodeMap)
    {
        foreach (var animation in root.LogicalAnimations)
        {
            var modelAnim = new RiggedModel.RiggedAnimation
            {
                AnimationName = animation.Name ?? string.Empty,
                TicksPerSecond = 1.0,
                DurationInTicks = animation.Duration,
                DurationInSeconds = animation.Duration,
                DurationInSecondsLooping = animation.Duration + AddedLoopingDuration,
                HasNodeAnimations = true,
                HasMeshAnimations = false,
                TotalFrames = (int)(animation.Duration * DefaultAnimatedFramesPerSecondLod),
                SecondsPerFrame = 1.0 / DefaultAnimatedFramesPerSecondLod,
                AnimatedNodes = new List<RiggedModel.RiggedAnimationNodes>(),
            };

            foreach (var (gltfNode, modelNode) in EnumerateNodeMap(nodeMap))
            {
                var nodeAnim = BuildAnimatedNode(animation, gltfNode, modelNode);
                if (nodeAnim != null)
                {
                    modelAnim.AnimatedNodes.Add(nodeAnim);
                }
            }

            model.OriginalAnimations.Add(modelAnim);
        }
    }

    private static IEnumerable<KeyValuePair<GltfNode, RiggedModel.RiggedModelNode>> EnumerateNodeMap(
        IReadOnlyDictionary<GltfNode, RiggedModel.RiggedModelNode> nodeMap)
    {
        return nodeMap;
    }

    private static RiggedModel.RiggedAnimationNodes BuildAnimatedNode(
        GltfAnimation animation,
        GltfNode gltfNode,
        RiggedModel.RiggedModelNode modelNode)
    {
        var translationChannel = animation.FindTranslationChannel(gltfNode);
        var rotationChannel = animation.FindRotationChannel(gltfNode);
        var scaleChannel = animation.FindScaleChannel(gltfNode);

        if (translationChannel == null && rotationChannel == null && scaleChannel == null)
        {
            return null;
        }

        var nodeAnim = new RiggedModel.RiggedAnimationNodes
        {
            Name = modelNode.Name,
            NodeRef = modelNode,
        };

        var bindTransform = gltfNode.LocalTransform;

        if (rotationChannel != null)
        {
            foreach (var (key, value) in rotationChannel.GetRotationSampler().GetLinearKeys())
            {
                nodeAnim.RotationTime.Add(key);
                nodeAnim.Rotation.Add(new Quaternion(value.X, value.Y, value.Z, value.W));
            }
        }
        if (nodeAnim.Rotation.Count == 0)
        {
            nodeAnim.RotationTime.Add(0.0);
            nodeAnim.Rotation.Add(new Quaternion(bindTransform.Rotation.X, bindTransform.Rotation.Y, bindTransform.Rotation.Z, bindTransform.Rotation.W));
        }

        if (translationChannel != null)
        {
            foreach (var (key, value) in translationChannel.GetTranslationSampler().GetLinearKeys())
            {
                nodeAnim.PositionTime.Add(key);
                nodeAnim.Position.Add(new Vector3(value.X, value.Y, value.Z));
            }
        }
        if (nodeAnim.Position.Count == 0)
        {
            nodeAnim.PositionTime.Add(0.0);
            nodeAnim.Position.Add(new Vector3(bindTransform.Translation.X, bindTransform.Translation.Y, bindTransform.Translation.Z));
        }

        if (scaleChannel != null)
        {
            foreach (var (key, value) in scaleChannel.GetScaleSampler().GetLinearKeys())
            {
                nodeAnim.ScaleTime.Add(key);
                nodeAnim.Scale.Add(new Vector3(value.X, value.Y, value.Z));
            }
        }
        if (nodeAnim.Scale.Count == 0)
        {
            nodeAnim.ScaleTime.Add(0.0);
            nodeAnim.Scale.Add(new Vector3(bindTransform.Scale.X, bindTransform.Scale.Y, bindTransform.Scale.Z));
        }

        return nodeAnim;
    }

    /// <summary>
    /// Mirrors the legacy loader's lookup: tries the path as given, then under the working
    /// directory's <c>Content</c>/<c>Assets</c> folders (the project asset path does not include
    /// the <c>Content</c> prefix that the content pipeline copies assets into).
    /// </summary>
    private static string ResolveModelPath(string filePathOrFileName)
    {
        if (string.IsNullOrWhiteSpace(filePathOrFileName) || File.Exists(filePathOrFileName))
        {
            return filePathOrFileName;
        }

        string relativeName = filePathOrFileName
            .Replace(Environment.CurrentDirectory, string.Empty)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var prefix in new[] { "Content", "Assets", Path.Combine("Content", "Assets") })
        {
            string candidate = Path.Combine(Environment.CurrentDirectory, prefix, relativeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return filePathOrFileName;
    }

    private static Matrix ToXna(System.Numerics.Matrix4x4 m)
    {
        return new Matrix(
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44);
    }
}
