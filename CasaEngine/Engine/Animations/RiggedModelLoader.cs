using Assimp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logger;

namespace CasaEngine.Engine.Animations;

// TODO's  see the model class for more.  
//
// Ok so it turns out that the animated bone transforms are doing some sort of reflection of the model which has a strange effect on the light calculations.
// just have to be aware of it.
//
//
// organize the reading order so it looks a little more readable and clear.
//
//
// read the material textures into the model ... edit... as lists.
// read the deformers
// read the colors its got a weird setup for that with channels.
// read all the values in the model for diffuse specular ect that is low priority most people will overide all that anyways in there own shader.
//
// tons of todos for the model need to be done as well.
// add back in errmm make a better bone visualization class because im going to want to edit animations later on.
// i really do not like blenders animation creator thing its wonky as hell  the principal stuff has already be setup for it in the model.
//
// One thing im definately going to do later is make a rescaler here because these models all seem to load with different sizes.
// Depending on who made them of course, even blender will rescale things funny.
// i still need to figure out how the mesh transform fits in to all this as it doesn't appear consistent doing a rescaler will mean it wont matter.
// The scaler will require that i recalculate the inverse bone offsets the local transforms and rescale all vertices.
// Before all that ill need to calculate the limits of all the vertices combined and then calculate the coefficients to the proportional scalars.
// this will need to be applied to pretty much everything except the rotations.
// then the inverse bind pose matrices for each bone will need to be recalculated.
// this surely belongs here not in the model class.

public class RiggedModelLoader
{
    Scene _scene;

    public static ContentManager Content;
    public static Effect EffectToUse;
    public static Texture2D DefaultTexture { get; set; }

    /// <summary>
    /// Reverses the models winding typically this will change the model vertices to counter clockwise winding ccw.
    /// </summary>
    public readonly bool ReverseVerticeWinding = false;

    /// <summary>
    /// Adds a small amount of additional looping time at the end of the time duration.
    /// This can help fix animations that are not properly or smoothly looped. 
    /// used in concert with AddedLoopingDuration
    /// </summary>
    public readonly bool AddAdditionalLoopingTime = true;
    /// <summary>
    /// Artificially adds a small amount of looping duration to the end of a animation. This helps to fix animations that aren't properly looped.
    /// Turn on AddAdditionalLoopingTime to use this.
    /// </summary>
    public readonly float AddedLoopingDuration = .5f;

    public readonly bool StartupConsoleinfo = true;
    public readonly bool StartupMinimalConsoleinfo = true;
    public readonly bool StartUpMatrixInfo = true;
    public readonly bool StartupAnimationConsoleInfo = false;
    public readonly bool StartupMaterialConsoleInfo = true;
    public bool StartupFlatBoneConsoleInfo = true;
    public readonly bool StartupNodeTreeConsoleInfo = true;
    public readonly string TargetNodeConsoleName = ""; //"L_Hand";



    int _defaultAnimatedFramesPerSecondLod = 24;

    /// <summary>
    /// Loading content here is just for visualizing but it wont be requisite if we load all the textures in from xnb's at runtime in completed model.
    /// </summary>
    public RiggedModelLoader(ContentManager content, Effect defaulteffect)
    {
        EffectToUse = defaulteffect;
        Content = content;
    }

    public RiggedModel LoadAsset(string filePathorFileName)
    {
        return LoadAsset(filePathorFileName, _defaultAnimatedFramesPerSecondLod);
    }

    public RiggedModel LoadAsset(string filePathorFileName, ContentManager content)
    {
        Content = content;
        return LoadAsset(filePathorFileName, _defaultAnimatedFramesPerSecondLod);
    }

    public RiggedModel LoadAsset(string filePathorFileName, int defaultAnimatedFramesPerSecondLod, ContentManager content)
    {
        Content = content;
        return LoadAsset(filePathorFileName, defaultAnimatedFramesPerSecondLod);
    }

    /// <summary> 
    /// Primary loading method. This method first looks in the Assets folder then in the Content folder for the file.
    /// If that fails it will look to see if the filepath is actually the full path to the file.
    /// The texture itself is expected to be loaded and then attached to the effect atm.
    /// </summary>
    public RiggedModel LoadAsset(string filePathorFileName, int defaultAnimatedFramesPerSecondLod)
    {
        _defaultAnimatedFramesPerSecondLod = defaultAnimatedFramesPerSecondLod;

        string s = Path.Combine(Environment.CurrentDirectory, "Assets", filePathorFileName);
        if (File.Exists(s) == false)
        {
            s = Path.Combine(Environment.CurrentDirectory, "Content", filePathorFileName);
        }

        if (File.Exists(s) == false)
        {
            s = Path.Combine(Environment.CurrentDirectory, "Content", "Assets", filePathorFileName);
        }

        if (File.Exists(s) == false)
        {
            s = Path.Combine(Environment.CurrentDirectory, filePathorFileName);
        }

        if (File.Exists(s) == false)
        {
            s = filePathorFileName;
        }

        Debug.Assert(File.Exists(s), "Could not find the file to load: " + s);
        string filepathorname = s;
        //
        // load the file at path to the scene
        //
        var importer = new AssimpContext();
        try
        {
            //LogManager.Instance.WriteLineTrace("(not sure this works) Model scale: " + importer.Scale);
            //importer.Scale = 1f / importer.Scale;
            //LogManager.Instance.WriteLineTrace("(not sure this works) Model scale: " + importer.Scale);

            _scene = importer.ImportFile(filepathorname,
                            PostProcessSteps.FlipUVs                // currently need
                                                | PostProcessSteps.JoinIdenticalVertices  // optimizes indexed
                                                | PostProcessSteps.Triangulate            // precaution
                                                | PostProcessSteps.FindInvalidData        // sometimes normals export wrong (remove & replace:)
                                                | PostProcessSteps.GenerateSmoothNormals  // smooths normals after identical verts removed (or bad normals)
                                                | PostProcessSteps.ImproveCacheLocality   // possible better cache optimization                                        
                                                | PostProcessSteps.FixInFacingNormals     // doesn't work well with planes - turn off if some faces go dark                                       
                                                | PostProcessSteps.CalculateTangentSpace  // use if you'll probably be using normal mapping 
                                                | PostProcessSteps.GenerateUVCoords       // useful for non-uv-map export primitives                                                
                                                | PostProcessSteps.ValidateDataStructure
                                                | PostProcessSteps.FindInstances
                                                | PostProcessSteps.GlobalScale            // use with AI_CONFIG_GLOBAL_SCALE_FACTOR_KEY (if need)                                                
                                                | PostProcessSteps.FlipWindingOrder       // (CCW to CW) Depends on your rasterizing setup (need clockwise to fix inside-out problem?)                                                 
            #region other_options
                                    //| PostProcessSteps.RemoveRedundantMaterials // use if not using material names to ID special properties                                                
                                    //| PostProcessSteps.FindDegenerates      // maybe (if using with AI_CONFIG_PP_SBP_REMOVE to remove points/lines)
                                    //| PostProcessSteps.SortByPrimitiveType  // maybe not needed (sort points, lines, etc)
                                    //| PostProcessSteps.OptimizeMeshes       // not suggested for animated stuff
                                    //| PostProcessSteps.OptimizeGraph        // not suggested for animated stuff                                        
                                    //| PostProcessSteps.TransformUVCoords    // maybe useful for keyed uv transforms                                                
            #endregion
                                    );
        }
        catch (Exception e)
        {
            LogManager.Instance.WriteLineTrace(e.Message);
            Debug.Assert(false, filePathorFileName + "\n\n" + "A problem loading the model occured: \n " + filePathorFileName + " \n" + e.Message);
            _scene = null;
        }

        return CreateModel(filepathorname);
    }

    /// <summary> Begins the flow to call methods and do the actual loading.
    /// </summary>
    private RiggedModel CreateModel(string filePath)
    {
        // create model
        RiggedModel model = new RiggedModel();

        // set the models effect to use.
        if (EffectToUse != null)
        {
            model.Effect = EffectToUse;
        }

        // prep to build a models tree.
        LogManager.Instance.WriteLineTrace("\n@@@CreateRootNode   prep to build a models tree. Set Up the Models RootNode");
        CreateRootNode(model, _scene);

        // create the models meshes
        LogManager.Instance.WriteLineTrace("\n@@@CreateModelMeshesSetUpMeshMaterialsAndTextures");
        CreateModelMeshesSetUpMeshMaterialsAndTextures(model, _scene, 0);

        // set up a dummy bone.
        LogManager.Instance.WriteLineTrace("\n@@@CreateDummyFlatListNodeZero");
        CreateDummyFlatListNodeZero(model);

        // recursively search and add the nodes to our model from the scene.
        LogManager.Instance.WriteLineTrace("\n@@@CreateModelNodeTreeTransformsRecursively");
        CreateModelNodeTreeTransformsRecursively(model, model.RootNodeOfTree, _scene.RootNode, 0);

        // find the actual and real first bone with a offset.
        LogManager.Instance.WriteLineTrace("\n@@@FindFirstBoneInModel");
        FindFirstBoneInModel(model, _scene.RootNode);

        // get the animations in the file into each nodes animations framelist
        LogManager.Instance.WriteLineTrace("\n@@@CreateOriginalAnimations\n");
        CreateOriginalAnimations(model, _scene);

        // this is the last thing we will do because we need the nodes set up first.

        // get the vertice data from the meshes.
        LogManager.Instance.WriteLineTrace("\n@@@CreateVerticeIndiceData");
        CreateVerticeIndiceData(model, _scene, 0);

        // this calls the models function to create the interpolated animtion frames.
        // for a full set of callable time stamped orientations per frame so indexing and dirty flags can be used when running.
        // im going to make this optional to were you don't have to use it there is a trade off either way you have to do look ups.
        // this way is a lot more memory but saves speed. 
        // the other way is a lot less memory but requires a lot more cpu calculations and twice as many look ups.
        //
        LogManager.Instance.WriteLineTrace("\n@@@model.CreateStaticAnimationLookUpFrames");
        model.CreateStaticAnimationLookUpFrames(_defaultAnimatedFramesPerSecondLod, AddAdditionalLoopingTime);

        LogManager.Instance.WriteLineTrace("\n@@@InfoFlatBones");
        InfoFlatBones(model);

        //// take a look at material information.
        if (StartupMaterialConsoleInfo)
        {
            LogManager.Instance.WriteLineTrace("\n@@@InfoForMaterials");
            InfoForMaterials(model, _scene);
        }

        // if we want to see the original animation data all this console crap is for debuging.
        if (StartupAnimationConsoleInfo)
        {
            LogManager.Instance.WriteLineTrace("\n@@@InfoForAnimData");
            InfoForAnimData(_scene);
        }

        if (StartupMinimalConsoleinfo)
        {
            MinimalInfo(model, filePath);
        }

        return model;
    }

    public void CreateRootNode(RiggedModel model, Scene scene)
    {
        model.RootNodeOfTree = new RiggedModel.RiggedModelNode();
        // set the rootnode and its transform
        model.RootNodeOfTree.Name = scene.RootNode.Name;
        // set the rootnode transforms
        model.RootNodeOfTree.LocalTransformMg = scene.RootNode.Transform.ToMonoGameTransposed();
        model.RootNodeOfTree.CombinedTransformMg = model.RootNodeOfTree.LocalTransformMg;
    }

    /// <summary>We create model mesh instances for each mesh in scene.meshes. This is just set up it doesn't load any data.
    /// </summary>
    public void CreateModelMeshesSetUpMeshMaterialsAndTextures(RiggedModel model, Scene scene, int meshIndex)
    {
        model.Meshes = new RiggedModel.RiggedModelMesh[scene.Meshes.Count];

        for (int index = 0; index < scene.Meshes.Count; index++)
        {
            Mesh mesh = scene.Meshes[index];
            var riggedModelMesh = new RiggedModel.RiggedModelMesh();
            riggedModelMesh.NameOfMesh = mesh.Name;
            riggedModelMesh.Texture = null; //DefaultTexture;
            riggedModelMesh.TextureName = "";
            //
            // The material used by this mesh.
            //A mesh uses only a single material. If an imported model uses multiple materials, the import splits up the mesh. Use this value as index into the scene's material list. 
            // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e
            //
            riggedModelMesh.MaterialIndex = mesh.MaterialIndex;
            if (StartupMaterialConsoleInfo)
            {
                LogManager.Instance.WriteLineTrace("scene.Meshes[" + index + "] " + " Material index: " + riggedModelMesh.MaterialIndex + " (material associated to this mesh)  " + "  Name " + mesh.Name);
            }

            model.Meshes[index] = riggedModelMesh;

            //for (int i = 0; i < scene.Materials.Count; i++)
            //{
            //if (i == riggedModelMesh.MaterialIndex)
            //{
            LogManager.Instance.WriteLineTrace("  Materials[" + riggedModelMesh.MaterialIndex + "]   get material textures");
            var material = scene.Materials[riggedModelMesh.MaterialIndex];

            //riggedModelMesh.ambient = assimpMaterial.ColorAmbient.ToMg();    // minimum light color
            //riggedModelMesh.diffuse = assimpMaterial.ColorDiffuse.ToMg();    // regular material colorization
            //riggedModelMesh.specular = assimpMaterial.ColorSpecular.ToMg();   // specular highlight color 
            //riggedModelMesh.emissive = assimpMaterial.ColorEmissive.ToMg();   // amplify a color brightness (not requiring light - similar to ambient really - kind of a glow without light)                 
            //riggedModelMesh.opacity = assimpMaterial.Opacity;                // how opaque or see-through is it? 
            //riggedModelMesh.reflectivity = assimpMaterial.Reflectivity;           // strength of reflections
            //riggedModelMesh.shininess = assimpMaterial.Shininess;              // how much light shines off
            //riggedModelMesh.shineStrength = assimpMaterial.ShininessStrength;      // probably specular power (can use to narrow & intensifies highlights - ie: more wet or metallic looking)
            //riggedModelMesh.bumpScale = assimpMaterial.BumpScaling;            // amplify or reduce normal-map effect
            //riggedModelMesh.isTwoSided = assimpMaterial.IsTwoSided;             // can be useful for glass or ice
            //not used
            //riggedModelMesh.colorTransparent   = assimpMaterial.ColorTransparent.ToMg();
            //riggedModelMesh.reflective         = assimpMaterial.ColorReflective.ToMg(); 
            //riggedModelMesh.transparency       = assimpMaterial.TransparencyFactor;
            //riggedModelMesh.hasShaders         = assimpMaterial.HasShaders;
            //riggedModelMesh.shadingMode        = assimpMaterial.ShadingMode.ToString();
            //riggedModelMesh.blendMode          = assimpMaterial.BlendMode.ToString();
            //riggedModelMesh.isPbrMaterial      = assimpMaterial.IsPBRMaterial;
            //riggedModelMesh.isWireFrameEnabled = assimpMaterial.IsWireFrameEnabled;

            var textureSlots = material.GetAllMaterialTextures();

            for (int j = 0; j < textureSlots.Length; j++)
            {
                var tindex = textureSlots[j].TextureIndex;
                var toperation = textureSlots[j].Operation;
                var ttype = textureSlots[j].TextureType.ToString();
                var tfilepath = textureSlots[j].FilePath;

                var tfilename = GetFileName(tfilepath, true);
                var tfullfilepath = Path.Combine(Content.RootDirectory, tfilename + ".xnb");
                var tfileexists = File.Exists(tfullfilepath);

                if (!tfileexists)
                {
                    var files = Directory.GetFiles(Content.RootDirectory, tfilename + ".xnb", SearchOption.AllDirectories);

                    if (files.Length > 0)
                    {
                        tfilename = files[0]
                            .Replace(Content.RootDirectory + Path.DirectorySeparatorChar, string.Empty)
                            .Replace(".xnb", string.Empty);
                        tfileexists = true;
                    }
                }

                if (StartupMaterialConsoleInfo)
                {
                    LogManager.Instance.WriteLineTrace("      Texture[" + j + "] " + "   Index: " + tindex.ToString().PadRight(5) + "   Type: " + ttype.PadRight(15) + "   Filepath: " + tfilepath.PadRight(15) + " Name: " + tfilename.PadRight(15) + "  ExistsInContent: " + tfileexists);
                }

                Texture2D texture = null;

                if (Content != null && tfileexists)
                {
                    texture = Content.Load<Texture2D>(tfilename);
                    LogManager.Instance.WriteLineTrace("      ...Texture loaded: ... " + tfilename);
                }

                if (ttype == "Diffuse")
                {
                    riggedModelMesh.TextureName = tfilename;
                    riggedModelMesh.Texture = texture;
                }
                else if (ttype == "Normal")
                {
                    riggedModelMesh.TextureNormalMapName = tfilename;
                    riggedModelMesh.TextureNormalMap = texture;
                }
                else if (ttype == "Height")
                {
                    riggedModelMesh.TextureHeightMapName = tfilename;
                    riggedModelMesh.TextureHeightMap = texture;
                }
                else if (ttype == "Reflection")
                {
                    riggedModelMesh.TextureReflectionMapName = tfilename;
                    riggedModelMesh.TextureReflectionMap = texture;
                }
            }
            //}
            //}
        }

    }

    /// <summary>  this isn't really necessary but i do it for debuging reasons. 
    /// </summary>
    public void CreateDummyFlatListNodeZero(RiggedModel model)
    {
        var modelnode = new RiggedModel.RiggedModelNode();
        modelnode.Name = "DummyBone0";
        // though we mark this false we add it to the flat bonenodes we index them via the bone count which is incremented below.
        modelnode.IsThisARealBone = false;
        modelnode.IsANodeAlongTheBoneRoute = false;
        modelnode.OffsetMatrixMg = Matrix.Identity;
        modelnode.LocalTransformMg = Matrix.Identity;
        modelnode.CombinedTransformMg = Matrix.Identity;
        modelnode.BoneShaderFinalTransformIndex = model.FlatListToBoneNodes.Count;
        model.FlatListToBoneNodes.Add(modelnode);
        model.NumberOfBonesInUse++;
    }

    /// <summary> We recursively walk the nodes here this drops all the info from scene nodes to model nodes.
    /// This gets offset matrices, if its a bone mesh index, global index, marks parents necessary ect..
    /// We mark neccessary also is this a bone, also is this part of a bone chain, children parents ect.
    /// Add node to model
    /// </summary>
    public void CreateModelNodeTreeTransformsRecursively(RiggedModel model, RiggedModel.RiggedModelNode modelnode, Node curAssimpNode, int tabLevel)
    {
        string ntab = "";
        for (int i = 0; i < tabLevel; i++)
            ntab += "  ";

        // set the nodes name.
        modelnode.Name = curAssimpNode.Name;
        // set the initial local node transform.
        modelnode.LocalTransformMg = curAssimpNode.Transform.ToMonoGameTransposed();

        if (StartupNodeTreeConsoleInfo)
        {
            Console.Write(ntab + "  Name: " + modelnode.Name);
        }

        // model structure creation building here.
        Point indexPair = SearchSceneMeshBonesForName(curAssimpNode.Name, _scene);
        // if the y value here is more then -1 this is then in fact a actual bone in the scene.
        if (indexPair.Y > -1)
        {
            if (StartupNodeTreeConsoleInfo)
            {
                Console.Write("  Is a Bone.  ");
            }

            // mark this a bone.
            modelnode.IsThisARealBone = true;
            // mark it a requisite transform node.
            modelnode.IsANodeAlongTheBoneRoute = true;
            // the offset bone matrix
            modelnode.OffsetMatrixMg = SearchSceneMeshBonesForNameGetOffsetMatrix(curAssimpNode.Name, _scene).ToMonoGameTransposed();
            // this maybe a bit redundant but i really don't care once i load it i can convert it to a more streamlined format later on.
            MarkParentsNessecary(modelnode);
            // we are about to add this now to the flat bone nodes list so also denote the index to the final shader transform.
            modelnode.BoneShaderFinalTransformIndex = model.FlatListToBoneNodes.Count;
            // necessary to keep things in order for the offsets as a way to just iterate thru bones and link to them thru a list.
            model.FlatListToBoneNodes.Add(modelnode);
            // increment the number of bones.
            model.NumberOfBonesInUse++;
        }

        // determines if this node is actually a mesh node.
        // if it is we need to then link the node to the mesh or the mesh to the node.
        if (curAssimpNode.HasMeshes)
        {
            modelnode.IsThisAMeshNode = true;
            // if its a node that represents a mesh it should also have references to a node for animations.
            if (StartupNodeTreeConsoleInfo)
            {
                Console.Write(" HasMeshes ... MeshIndices For This Node:  ");
            }

            // the mesh node doesn't normally have or need a bind pose matrix however im going to make one here because im actually going to need it.
            // for complex mesh with bone animations were they are both in the same animation.
            modelnode.InvOffsetMatrixMg = modelnode.LocalTransformMg.Invert();

            // since i already copied over the meshes then i should set the meshes listed to have this node as the reference node.
            foreach (var mi in curAssimpNode.MeshIndices)
            {
                if (StartupNodeTreeConsoleInfo)
                {
                    Console.Write("  mesh[" + mi + "] nameOfMesh: " + model.Meshes[mi].NameOfMesh);
                }

                // get the applicable model mesh reference.
                var m = model.Meshes[mi];
                // set the current node reference to each applicable mesh node ref that uses it so each meshes can reference it' the node transform.
                m.NodeRefContainingAnimatedTransform = modelnode;
                // set the mesh original local transform.
                if (StartupNodeTreeConsoleInfo)
                {
                    if (modelnode.IsThisARealBone)
                    {
                        Console.Write("  LinkedNodesOffset IsABone: ");
                    }
                    //Console.GenerateClassCode("  LinkedNodesOffset: " + modelnode.OffsetMatrixMg.ToAssimpTransposed().SrtInfoToString(""));
                }
                m.MeshInitialTransformFromNodeMg = m.NodeRefContainingAnimatedTransform.LocalTransformMg;
                m.MeshCombinedFinalTransformMg = Matrix.Identity;

                if (StartupNodeTreeConsoleInfo)
                {
                    Console.Write(" " + " Is a mesh ... Mesh nodeReference Set.");
                }
            }
        }
        if (StartupNodeTreeConsoleInfo && StartUpMatrixInfo)
        {
            LogManager.Instance.WriteLineTrace("");
            string ntab2 = ntab + "    ";
            LogManager.Instance.WriteLineTrace(ntab2 + "curAssimpNode.Transform: " + curAssimpNode.Transform.SrtInfoToString(ntab2));
        }

        // add node to flat node list
        model.FlatListToAllNodes.Add(modelnode);
        model.NumberOfNodesInUse++;

        // access children
        for (int i = 0; i < curAssimpNode.Children.Count; i++)
        {
            var childAsimpNode = curAssimpNode.Children[i];
            var childBoneNode = new RiggedModel.RiggedModelNode();
            // set parent before passing.
            childBoneNode.Parent = modelnode;
            childBoneNode.Name = curAssimpNode.Children[i].Name;
            if (childBoneNode.Parent.IsANodeAlongTheBoneRoute)
            {
                childBoneNode.IsANodeAlongTheBoneRoute = true;
            }

            modelnode.Children.Add(childBoneNode);
            CreateModelNodeTreeTransformsRecursively(model, modelnode.Children[i], childAsimpNode, tabLevel + 1);
        }
    }

    /// <summary>Get Scene Model Mesh Vertices. Gets all the mesh data into a mesh array. 
    /// </summary>
    public void CreateVerticeIndiceData(RiggedModel model, Scene scene, int meshIndex) // RiggedModel
    {
        // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e

        //
        // Loop meshes for Vertice data.
        //
        for (int mloop = 0; mloop < scene.Meshes.Count; mloop++)
        {
            Mesh mesh = scene.Meshes[mloop];
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace(
                "\n" + "__________________________" +
                "\n" + "scene.Meshes[" + mloop + "] " +
                "\n" + " FaceCount: " + mesh.FaceCount +
                "\n" + " VertexCount: " + mesh.VertexCount +
                "\n" + " Normals.Count: " + mesh.Normals.Count +
                "\n" + " BoneCount: " + mesh.BoneCount +
                "\n" + " MaterialIndex: " + mesh.MaterialIndex
                );
                LogManager.Instance.WriteLineTrace("  mesh.UVComponentCount.Length: " + mesh.UVComponentCount.Length);
            }
            for (int i = 0; i < mesh.UVComponentCount.Length; i++)
            {
                int val = mesh.UVComponentCount[i];
                if (StartupConsoleinfo)
                {
                    LogManager.Instance.WriteLineTrace("       mesh.UVComponentCount[" + i + "] : " + val);
                }
            }

            // indices
            int[] indexs = new int[mesh.Faces.Count * 3];
            int loopindex = 0;
            for (int k = 0; k < mesh.Faces.Count; k++)
            {
                var f = mesh.Faces[k];
                for (int j = 0; j < f.IndexCount; j++)
                {
                    var ind = f.Indices[j];
                    indexs[loopindex] = ind;
                    loopindex++;
                }
            }

            // vertices 
            VertexPositionTextureNormalTangentWeights[] v = new VertexPositionTextureNormalTangentWeights[mesh.Vertices.Count];
            for (int k = 0; k < mesh.Vertices.Count; k++)
            {
                var f = mesh.Vertices[k];
                v[k].Position = new Vector3(f.X, f.Y, f.Z);
            }
            // normals
            for (int k = 0; k < mesh.Normals.Count; k++)
            {
                var f = mesh.Normals[k];
                v[k].Normal = new Vector3(f.X, f.Y, f.Z);
            }

            // Check whether the mesh contains tangent and bitangent vectors It is not possible that it contains tangents and no bitangents (or the other way round). 
            // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e
            //

            //// TODO need to add this to the vertex declaration or calculate it on the shader.

            // tangents
            for (int k = 0; k < mesh.Tangents.Count; k++)
            {
                var f = mesh.Tangents[k];
                v[k].Tangent = new Vector3(f.X, f.Y, f.Z);
            }
            // bi tangents  
            for (int k = 0; k < mesh.BiTangents.Count; k++)
            {
                var f = mesh.BiTangents[k];
                v[k].Tangent = f.ToMonoGame();
            }

            // A mesh may contain 0 to AI_MAX_NUMBER_OF_COLOR_SETS vertex colors per vertex. NULL if not present. Each array is mNumVertices in size if present. 
            // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e

            // TODO colors dunno why there are lists of lists for colors 
            // maybe its multi colored or something ill have to read up on this ...  not sure this is the right way to do it ?
            //  This will have to be made from scratch need v4 to mg and other stuff
            //
            if (mesh.HasVertexColors(0))
            {
                var cchan0 = mesh.VertexColorChannels[0];
                for (int k = 0; k < cchan0.Count; k++)
                {
                    //var f = mesh.VertexColorChannels[k];
                    Vector4 cf;
                    for (int i = 0; i < cchan0.Count; i++)
                    {
                        var cc = cchan0[i];
                        cf = new Vector4(cc.R, cc.G, cc.B, cc.A);
                        v[i].Color = cf;
                    }
                }
            }
            else
            {
                for (int k = 0; k < mesh.VertexColorChannels[0].Count; k++)
                    v[k].Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            }


            // Check whether the mesh contains a texture coordinate set. 
            // mNumUVComponents
            // unsigned int aiMesh::mNumUVComponents[AI_MAX_NUMBER_OF_TEXTURECOORDS]
            // Specifies the number of components for a given UV channel.
            // Up to three channels are supported(UVW, for accessing volume or cube maps).If the value is 2 for a given channel n, the component p.z of mTextureCoords[n][p] is set to 0.0f.If the value is 1 for a given channel, p.y is set to 0.0f, too.
            // Note 4D coords are not supported

            // Uv
            LogManager.Instance.WriteLineTrace("");
            var uvchannels = mesh.TextureCoordinateChannels;
            for (int k = 0; k < uvchannels.Length; k++)
            {
                var f = uvchannels[k];
                int loopIndex = 0;
                for (int j = 0; j < f.Count; j++)
                {
                    var uv = f[j];
                    v[loopIndex].TextureCoordinate = new Vector2(uv.X, uv.Y);
                    loopIndex++;
                }
            }

            // find the min max vertices for a bounding box.
            // this is useful for other stuff which i need right now.

            Vector3 min = Vector3.Zero;
            Vector3 max = Vector3.Zero;
            Vector3 centroid = Vector3.Zero;
            foreach (var vert in v)
            {
                if (vert.Position.X < min.X) { min.X = vert.Position.X; }
                if (vert.Position.Y < min.Y) { min.Y = vert.Position.Y; }
                if (vert.Position.Z < min.Z) { min.Z = vert.Position.Z; }
                if (vert.Position.X > max.X) { max.X = vert.Position.X; }
                if (vert.Position.Y > max.Y) { max.Y = vert.Position.Y; }
                if (vert.Position.Z > max.Z) { max.Z = vert.Position.Z; }
                centroid += vert.Position;
            }
            model.Meshes[mloop].Centroid = centroid / (float)v.Length;
            model.Meshes[mloop].Min = min;
            model.Meshes[mloop].Max = max;

            // Prep blend weight and indexs this one is just prep for later on.
            for (int k = 0; k < mesh.Vertices.Count; k++)
            {
                var f = mesh.Vertices[k];
                v[k].BlendIndices = new Vector4(0f, 0f, 0f, 0f);
                v[k].BlendWeights = new Vector4(0f, 0f, 0f, 0f);
            }

            // Restructure vertice data to conform to a shader.
            // Iterate mesh bone offsets set the bone Id's and weights to the vertices.
            // This also entails correlating the mesh local bone index names to the flat bone list.
            TempWeightVert[] verts = new TempWeightVert[mesh.Vertices.Count];
            if (mesh.HasBones)
            {
                var meshBones = mesh.Bones;
                if (StartupConsoleinfo)
                {
                    LogManager.Instance.WriteLineTrace("meshBones.Count: " + meshBones.Count);
                }

                for (int meshBoneIndex = 0; meshBoneIndex < meshBones.Count; meshBoneIndex++)
                {
                    var boneInMesh = meshBones[meshBoneIndex]; // ahhhh
                    var boneInMeshName = meshBones[meshBoneIndex].Name;
                    var correspondingFlatBoneListIndex = GetFlatBoneIndexInModel(model, scene, boneInMeshName);

                    if (StartupConsoleinfo)
                    {
                        string str = "  mesh.Name: " + mesh.Name + "mesh[" + mloop + "] " + " bone.Name: " + boneInMeshName.PadRight(17) + "     meshLocalBoneListIndex: " + meshBoneIndex.ToString().PadRight(4) + " flatBoneListIndex: " + correspondingFlatBoneListIndex.ToString().PadRight(4) + " WeightCount: " + boneInMesh.VertexWeightCount;
                        LogManager.Instance.WriteLineTrace(str);
                    }

                    // loop thru this bones vertice listings with the weights for it.
                    for (int weightIndex = 0; weightIndex < boneInMesh.VertexWeightCount; weightIndex++)
                    {
                        var verticeIndexTheBoneIsFor = boneInMesh.VertexWeights[weightIndex].VertexID;
                        var boneWeightVal = boneInMesh.VertexWeights[weightIndex].Weight;
                        if (verts[verticeIndexTheBoneIsFor] == null)
                        {
                            verts[verticeIndexTheBoneIsFor] = new TempWeightVert();
                        }
                        // add this vertice its weight and the bone id to the temp verts list.
                        verts[verticeIndexTheBoneIsFor].VerticeIndexs.Add(verticeIndexTheBoneIsFor);
                        verts[verticeIndexTheBoneIsFor].VerticesFlatBoneId.Add(correspondingFlatBoneListIndex);
                        verts[verticeIndexTheBoneIsFor].VerticeBoneWeights.Add(boneWeightVal);
                        verts[verticeIndexTheBoneIsFor].CountOfBoneEntrysForThisVertice++;
                    }
                }
            }
            else // mesh has no bones
            {
                // if there is no bone data we will make it set to bone zero.
                // this is basically a safety measure as if there is no bone data there is no bones.
                // however the vertices need to have a weight of 1.0 for bone zero which is really identity.
                for (int i = 0; i < verts.Length; i++)
                {
                    verts[i] = new TempWeightVert();
                    var ve = verts[i];
                    if (ve.VerticeIndexs.Count == 0)
                    {
                        // there is no bone data for this vertice at all then we should set it to bone zero.
                        verts[i].VerticeIndexs.Add(i);
                        verts[i].VerticesFlatBoneId.Add(0);
                        verts[i].VerticeBoneWeights.Add(1.0f);
                    }
                }
            }

            // Ill need up to 4 values per bone list so if some of the values are empty ill copy zero to them with weight 0.
            // This is to ensure the full key vector4 is populated.
            // The bone weight data aligns to the bones not nodes so it aligns to the offset matrices bone names.

            // loop each temp vertice add the temporary structure we have to the model vertices in sequence.
            for (int i = 0; i < verts.Length; i++)
            {
                if (verts[i] != null)
                {
                    var ve = verts[i];
                    //int maxbones = 4;
                    var arrayIndex = ve.VerticeIndexs.ToArray();
                    var arrayBoneId = ve.VerticesFlatBoneId.ToArray();
                    var arrayWeight = ve.VerticeBoneWeights.ToArray();
                    if (arrayBoneId.Count() > 3)
                    {
                        v[arrayIndex[3]].BlendIndices.W = arrayBoneId[3];
                        v[arrayIndex[3]].BlendWeights.W = arrayWeight[3];
                    }
                    if (arrayBoneId.Count() > 2)
                    {
                        v[arrayIndex[2]].BlendIndices.Z = arrayBoneId[2];
                        v[arrayIndex[2]].BlendWeights.Z = arrayWeight[2];
                    }
                    if (arrayBoneId.Count() > 1)
                    {
                        v[arrayIndex[1]].BlendIndices.Y = arrayBoneId[1];
                        v[arrayIndex[1]].BlendWeights.Y = arrayWeight[1];
                    }
                    if (arrayBoneId.Count() > 0)
                    {
                        v[arrayIndex[0]].BlendIndices.X = arrayBoneId[0];
                        v[arrayIndex[0]].BlendWeights.X = arrayWeight[0];
                    }
                }
            }

            model.Meshes[mloop].Vertices = v;
            model.Meshes[mloop].Indices = indexs;

            // last thing reverse the winding if specified.
            if (ReverseVerticeWinding)
            {
                for (int k = 0; k < model.Meshes[mloop].Indices.Length; k += 3)
                {
                    var i0 = model.Meshes[mloop].Indices[k + 0];
                    var i1 = model.Meshes[mloop].Indices[k + 1];
                    var i2 = model.Meshes[mloop].Indices[k + 2];
                    model.Meshes[mloop].Indices[k + 0] = i0;
                    model.Meshes[mloop].Indices[k + 1] = i2;
                    model.Meshes[mloop].Indices[k + 2] = i1;
                }
            }

        }
        //return model;
    }

    /// <summary> Gets the assimp animations as the original does it into the model.
    /// </summary>
    public void CreateOriginalAnimations(RiggedModel model, Scene scene)
    {
        // Nice now i find it after i already figured it out.
        // http://sir-kimmi.de/assimp/lib_html/_animation_overview.html
        // http://sir-kimmi.de/assimp/lib_html/structai_animation.html
        // http://sir-kimmi.de/assimp/lib_html/structai_anim_mesh.html
        // Animations

        // Copy over as assimp has it set up.
        for (int i = 0; i < scene.Animations.Count; i++)
        {
            var assimpAnim = scene.Animations[i];
            //________________________________________________
            // Initial copy over.
            var modelAnim = new RiggedModel.RiggedAnimation();
            modelAnim.AnimationName = assimpAnim.Name;
            modelAnim.TicksPerSecond = assimpAnim.TicksPerSecond;
            modelAnim.DurationInTicks = assimpAnim.DurationInTicks;
            modelAnim.DurationInSeconds = assimpAnim.DurationInTicks / assimpAnim.TicksPerSecond;
            if (AddAdditionalLoopingTime)
            {
                modelAnim.DurationInSecondsLooping = modelAnim.DurationInSeconds + AddedLoopingDuration;
            }
            else
            {
                modelAnim.DurationInSecondsLooping = modelAnim.DurationInSeconds;
            }

            // Default.
            modelAnim.TotalFrames = (int)(modelAnim.DurationInSeconds * (double)(_defaultAnimatedFramesPerSecondLod));
            modelAnim.TicksPerFramePerSecond = modelAnim.TicksPerSecond / (double)(_defaultAnimatedFramesPerSecondLod);
            modelAnim.SecondsPerFrame = (1d / (_defaultAnimatedFramesPerSecondLod));
            //
            modelAnim.HasNodeAnimations = assimpAnim.HasNodeAnimations;
            modelAnim.HasMeshAnimations = assimpAnim.HasMeshAnimations;
            // 
            // create new animation node list per animation
            modelAnim.AnimatedNodes = new List<RiggedModel.RiggedAnimationNodes>();
            // Loop the node channels.
            for (int j = 0; j < assimpAnim.NodeAnimationChannels.Count; j++)
            {
                var nodeAnimLists = assimpAnim.NodeAnimationChannels[j];
                var nodeAnim = new RiggedModel.RiggedAnimationNodes();
                nodeAnim.NodeName = nodeAnimLists.NodeName;

                // Set the reference to the node for node name by the model method that searches for it.
                var modelnoderef = ModelGetRefToNode(nodeAnimLists.NodeName, model.RootNodeOfTree);
                //var modelnoderef = model.SearchNodeTreeByNameGetRefToNode(nodeAnimLists.NodeName);
                nodeAnim.NodeRef = modelnoderef;

                // Place all the different keys lists rot scale pos into this nodes elements lists.
                foreach (var keyList in nodeAnimLists.RotationKeys)
                {
                    var oaq = keyList.Value;
                    nodeAnim.QrotTime.Add(keyList.Time / assimpAnim.TicksPerSecond);
                    nodeAnim.Qrot.Add(oaq.ToMonoGame());
                }
                foreach (var keyList in nodeAnimLists.PositionKeys)
                {
                    var oap = keyList.Value.ToMonoGame();
                    nodeAnim.PositionTime.Add(keyList.Time / assimpAnim.TicksPerSecond);
                    nodeAnim.Position.Add(oap);
                }
                foreach (var keyList in nodeAnimLists.ScalingKeys)
                {
                    var oas = keyList.Value.ToMonoGame();
                    nodeAnim.ScaleTime.Add(keyList.Time / assimpAnim.TicksPerSecond);
                    nodeAnim.Scale.Add(oas);
                }
                // Place this populated node into this model animation,  model.origAnim
                modelAnim.AnimatedNodes.Add(nodeAnim);
            }
            // Place the animation into the model.
            model.OriginalAnimations.Add(modelAnim);
        }
        //return model;
    }

    /*  well need this later on if we want these other standard types of animations
            LogManager.Instance.WriteLineTrace($"  HasMeshAnimations: {anim.HasMeshAnimations} ");
            LogManager.Instance.WriteLineTrace($"  Mesh Animation Channels: {anim.MeshAnimationChannelCount} ");
            foreach (var chan in anim.MeshAnimationChannels)
            {
                LogManager.Instance.WriteLineTrace($"  Channel MeshName {chan.MeshName}");        // the node name has to be used to tie this channel to the originally printed hierarchy.  BTW, node names must be unique.
                LogManager.Instance.WriteLineTrace($"    HasMeshKeys: {chan.HasMeshKeys}");       // access via chan.PositionKeys
                LogManager.Instance.WriteLineTrace($"    MeshKeyCount: {chan.MeshKeyCount}");       // 
                //LogManager.Instance.WriteLineTrace($"    Scaling  Keys: {chan.MeshKeys}");        // 
            }
            LogManager.Instance.WriteLineTrace($"  Mesh Morph Channels: {anim.MeshMorphAnimationChannelCount} ");
            foreach (var chan in anim.MeshMorphAnimationChannels)
            {
                LogManager.Instance.WriteLineTrace($"  Channel {chan.Name}");
                LogManager.Instance.WriteLineTrace($"    HasMeshMorphKeys: {chan.HasMeshMorphKeys}");       // 
                LogManager.Instance.WriteLineTrace($"     MeshMorphKeyCount: {chan.MeshMorphKeyCount}");       // 
                //LogManager.Instance.WriteLineTrace($"    Scaling  Keys: {chan.MeshMorphKeys}");        // 
            }
     */


    /*
       ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
       ++++++++++++++++++++ Additional functions ++++++++++++++++++++
       ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    */

    /// <summary> Custom get file name.
    /// </summary>
    public string GetFileName(string s, bool useBothSeperators)
    {
        var tpathsplit = s.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        string f = tpathsplit[0];
        if (tpathsplit.Length > 1)
        {
            f = tpathsplit[^2];
        }
        if (useBothSeperators)
        {
            tpathsplit = f.Split(new char[] { '/', '\\' });
        }
        else
        {
            tpathsplit = f.Split(new char[] { '/' });
        }

        s = tpathsplit[^1];
        return s.TrimStart('\\');
    }

    /// <summary>We Mark Parents Nessecary down the chain till we hit null this is because the parent node matrices are required for the transformation chain.
    /// </summary>
    public void MarkParentsNessecary(RiggedModel.RiggedModelNode b)
    {
        b.IsThisNodeTransformNecessary = true;
        b.IsANodeAlongTheBoneRoute = true;
        if (b.Parent != null)
        {
            MarkParentsNessecary(b.Parent);
        }
        else
        {
            b.IsTheRootNode = true;
        }
    }

    /// <summary>We Mark Parents Un Nessecary down the chain till we hit null this is for when the parent nodes are not used before the root bone.
    /// </summary>
    public void MarkParentsUnNessecary(RiggedModel.RiggedModelNode b)
    {
        b.IsThisNodeTransformNecessary = false;
        if (b.Parent != null)
        {
            MarkParentsUnNessecary(b.Parent);
        }
    }

    /// <summary>Not sure how much this is needed if at all but this marks the first root bone in the model.
    /// </summary>
    public void FindFirstBoneInModel(RiggedModel model, Node node)
    {
        bool result = false;
        Point indexPair = SearchSceneMeshBonesForName(node.Name, _scene);
        if (indexPair.Y > -1)
        {
            result = true;
            model.FirstRealBoneInTree = SearchAssimpNodesForName(node.Name, model.RootNodeOfTree);
            model.FirstRealBoneInTree.IsTheFirstBone = true;
            //model.globalPreTransformNode = model.firstRealBoneInTree.parent; // this is pointles here initially done due to bad advice from a site.
            //model.globalPreTransformNode.isTheGlobalPreTransformNode = true; // this is pointles here initially done due to bad advice from a site.
        }
        else
        {
            foreach (var c in node.Children)
            {
                FindFirstBoneInModel(model, c);
            }
        }
    }

    /// <summary> Gets the index to the flat bone from its node name. 
    /// </summary>
    public int GetFlatBoneIndexInModel(RiggedModel model, Scene scene, string nameToFind)
    {
        int index = -1;
        for (int i = 0; i < model.FlatListToBoneNodes.Count; i++)
        {
            var n = model.FlatListToBoneNodes[i];
            if (n.Name == nameToFind)
            {
                index = i;
                i = model.FlatListToBoneNodes.Count; // break
            }
        }
        if (index == -1)
        {
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace("**** No Index found for the named bone (" + nameToFind + ") this is not good ");
            }
        }
        return index;
    }


    /// <summary>Returns X as the mesh index and Y as the bone number if Y is negative it is not a bone.
    /// </summary>
    public Point SearchSceneMeshBonesForName(string name, Scene scene)
    {
        Point result = new Point(-1, -1);
        bool found = false;
        for (int i = 0; i < scene.Meshes.Count; i++)
        {
            for (int j = 0; j < scene.Meshes[i].Bones.Count; j++)
            {
                if (scene.Meshes[i].Bones[j].Name == name)
                {
                    result = new Point(i, j);
                    found = true;
                    j = scene.Meshes[i].Bones.Count;
                }
            }
            if (found)
            {
                i = scene.Meshes.Count;
            }
        }
        return result;
    }

    /// <summary>
    /// Returns X as the mesh index and Y as the bone number if Y is negative it is not a bone.
    /// </summary>
    public Matrix4x4 SearchSceneMeshBonesForNameGetOffsetMatrix(string name, Scene scene)
    {
        Matrix4x4 result = Matrix4x4.Identity;
        bool found = false;
        for (int i = 0; i < scene.Meshes.Count; i++)
        {
            for (int j = 0; j < scene.Meshes[i].Bones.Count; j++)
            {
                if (scene.Meshes[i].Bones[j].Name == name)
                {
                    result = scene.Meshes[i].Bones[j].OffsetMatrix;//MatrixConvertAssimpToMg(scene.Meshes[i].Bones[j].OffsetMatrix);
                    found = true;
                    j = scene.Meshes[i].Bones.Count;
                }
            }
            if (found)
            {
                i = scene.Meshes.Count;
            }
        }
        return result;
    }

    /// <summary>
    /// Finds the model node with the bone name.
    /// </summary>
    public RiggedModel.RiggedModelNode SearchAssimpNodesForName(string name, RiggedModel.RiggedModelNode node)
    {
        RiggedModel.RiggedModelNode result = null;
        if (node.Name == name)
        {
            result = node;
        }
        if (result == null && node.Children.Count > 0)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                var res = SearchAssimpNodesForName(name, node.Children[i]);
                if (res != null)
                {
                    result = res;
                    i = node.Children.Count;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// </summary>
    public RiggedModel.RiggedModelNode SearchNodeTreeByNameGetRefToNode(string name, RiggedModel.RiggedModelNode rootNodeOfTree) // , OnlyAssimpBasedModel model );
    {
        return SearchIterateNodeTreeForNameGetRefToNode(name, rootNodeOfTree);
    }

    /// <summary>
    /// </summary>
    private RiggedModel.RiggedModelNode SearchIterateNodeTreeForNameGetRefToNode(string name, RiggedModel.RiggedModelNode node)
    {
        RiggedModel.RiggedModelNode result = null;
        if (node.Name == name)
        {
            result = node;
        }

        if (result == null && node.Children.Count > 0)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                var res = SearchIterateNodeTreeForNameGetRefToNode(name, node.Children[i]);
                if (res != null)
                {
                    // set result and break if the named node was found
                    result = res;
                    i = node.Children.Count;
                }
            }
        }
        return result;
    }



    /// <summary>
    /// Argg pretty much useless the way assimp does it this was a boo boo
    /// returns a reference to the mesh that matches the named mesh.
    /// returns null if no match found.
    /// </summary>
    private RiggedModel.RiggedModelMesh SearchModelMeshesForNameGetRefToMesh(string name, RiggedModel model)
    {
        RiggedModel.RiggedModelMesh result = null;
        for (int j = 0; j < model.Meshes.Length; j++)
        {
            var m = model.Meshes[j];
            if (name == m.NameOfMesh)
            {
                result = model.Meshes[j];
            }
        }
        return result;
    }

    /// <summary>
    /// Same as ModelSearchIterateNodeTreeForNameGetRefToNode
    /// </summary>
    public static RiggedModel.RiggedModelNode ModelGetRefToNode(string name, RiggedModel.RiggedModelNode rootNodeOfTree) // , OnlyAssimpBasedModel model );
    {
        return ModelSearchIterateNodeTreeForNameGetRefToNode(name, rootNodeOfTree);
    }

    /// <summary>
    /// Searches the model for the name of the node if found it returns the model node if not it returns null.
    /// </summary>
    private static RiggedModel.RiggedModelNode ModelSearchIterateNodeTreeForNameGetRefToNode(string name, RiggedModel.RiggedModelNode node)
    {
        RiggedModel.RiggedModelNode result = null;
        if (node.Name == name)
        {
            result = node;
        }

        if (result == null && node.Children.Count > 0)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                var res = ModelSearchIterateNodeTreeForNameGetRefToNode(name, node.Children[i]);
                if (res != null)
                {
                    // set result and break if the named node was found
                    result = res;
                    i = node.Children.Count;
                }
            }
        }
        return result;
    }


    public void MinimalInfo(RiggedModel model, string filePath)
    {
        LogManager.Instance.WriteLineTrace("\n");
        LogManager.Instance.WriteLineTrace("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
        LogManager.Instance.WriteLineTrace("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
        LogManager.Instance.WriteLineTrace("Model Loaded");
        LogManager.Instance.WriteLineTrace("");
        LogManager.Instance.WriteLineTrace(filePath);
        LogManager.Instance.WriteLineTrace("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
        LogManager.Instance.WriteLineTrace("Materials");
        LogManager.Instance.WriteLineTrace("");
        InfoForMaterials(model, _scene);
        LogManager.Instance.WriteLineTrace("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
        LogManager.Instance.WriteLineTrace("Animations");
        LogManager.Instance.WriteLineTrace("");
        for (int i = 0; i < _scene.Animations.Count; i++)
        {
            var anim = _scene.Animations[i];
            LogManager.Instance.WriteLineTrace($"_____________________________________");
            LogManager.Instance.WriteLineTrace($"Anim #[{i}] Name: {anim.Name}");
            LogManager.Instance.WriteLineTrace($"_____________________________________");
            LogManager.Instance.WriteLineTrace($"  Duration: {anim.DurationInTicks} / {anim.TicksPerSecond} sec.   total duration in seconds: {anim.DurationInTicks / anim.TicksPerSecond}");
            LogManager.Instance.WriteLineTrace($"  Node Animation Channels: {anim.NodeAnimationChannelCount} ");
            LogManager.Instance.WriteLineTrace($"  Mesh Animation Channels: {anim.MeshAnimationChannelCount} ");
            LogManager.Instance.WriteLineTrace($"  Mesh Morph     Channels: {anim.MeshMorphAnimationChannelCount} ");
        }
        LogManager.Instance.WriteLineTrace("");
        LogManager.Instance.WriteLineTrace("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
        LogManager.Instance.WriteLineTrace("Node Heirarchy");
        LogManager.Instance.WriteLineTrace("");
        InfoRiggedModelNode(model.RootNodeOfTree, 0);
        LogManager.Instance.WriteLineTrace("");
        LogManager.Instance.WriteLineTrace("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
        LogManager.Instance.WriteLineTrace($"Model");
        LogManager.Instance.WriteLineTrace($"{GetFileName(filePath, true)} Loaded");
        LogManager.Instance.WriteLineTrace("");
        LogManager.Instance.WriteLineTrace("Model number of bones:    " + (model.NumberOfBonesInUse - 1).ToString() + " +1 dummy bone"); // -1 dummy bone.
        LogManager.Instance.WriteLineTrace("Model number of animaton: " + model.OriginalAnimations.Count);
        LogManager.Instance.WriteLineTrace("Model number of meshes:   " + model.Meshes.Length);
        LogManager.Instance.WriteLineTrace("BoneRoot's Node Name:     " + model.RootNodeOfTree.Name);
        LogManager.Instance.WriteLineTrace("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
        LogManager.Instance.WriteLineTrace("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
        LogManager.Instance.WriteLineTrace("\n");
    }
    public void InfoRiggedModelNode(RiggedModel.RiggedModelNode n, int tabLevel)
    {
        string ntab = "";
        for (int i = 0; i < tabLevel; i++)
            ntab += "  ";

        string msg = ntab + $"Name: {n.Name}  ".PadRight(30) + " ";
        if (n.IsTheRootNode)
        {
            msg += $", isTheRootNode".PadRight(20);
        }

        if (n.IsThisARealBone)
        {
            msg += $", isARealBone".PadRight(20);
        }

        if (n.IsTheFirstBone)
        {
            msg += $", isTheFirstBone".PadRight(20);
        }

        if (n.IsANodeAlongTheBoneRoute)
        {
            msg += $", isAlongTheBoneRoute".PadRight(20);
        }

        if (n.IsThisAMeshNode)
        {
            msg += $", isMeshNode".PadRight(20);
        }

        if (n.IsThisTheFirstMeshNode)
        {
            msg += $", isTheFirstMeshNode".PadRight(20);
        }

        LogManager.Instance.WriteLineTrace(msg);

        for (int i = 0; i < n.Children.Count; i++)
        {
            InfoRiggedModelNode(n.Children[i], tabLevel + 1);
        }
    }

    /// <summary>
    /// </summary>
    public void InfoForAnimData(Scene scene)
    {
        //int i;
        if (StartupConsoleinfo)
        {
            string str = "\n\n AssimpSceneConsoleOutput ========= Animation Data========= \n\n";
            LogManager.Instance.WriteLineTrace(str);
        }

        for (int i = 0; i < scene.Animations.Count; i++)
        {
            var anim = scene.Animations[i];
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace($"_________________________________");
                LogManager.Instance.WriteLineTrace($"Anim #[{i}] Name: {anim.Name}");
                LogManager.Instance.WriteLineTrace($"_________________________________");
                LogManager.Instance.WriteLineTrace($"  Duration: {anim.DurationInTicks} / {anim.TicksPerSecond} sec.   total duration in seconds: {anim.DurationInTicks / anim.TicksPerSecond}");
                LogManager.Instance.WriteLineTrace($"  HasMeshAnimations: {anim.HasMeshAnimations} ");
                LogManager.Instance.WriteLineTrace($"  Mesh Animation Channels: {anim.MeshAnimationChannelCount} ");
            }
            foreach (var chan in anim.MeshAnimationChannels)
            {
                if (StartupConsoleinfo)
                {
                    LogManager.Instance.WriteLineTrace($"  Channel MeshName {chan.MeshName}");        // the node name has to be used to tie this channel to the originally printed hierarchy.  BTW, node names must be unique.
                    LogManager.Instance.WriteLineTrace($"    HasMeshKeys: {chan.HasMeshKeys}");       // access via chan.PositionKeys
                    LogManager.Instance.WriteLineTrace($"    MeshKeyCount: {chan.MeshKeyCount}");       // 
                                                                                                        //LogManager.Instance.WriteLineTrace($"    Scaling  Keys: {chan.MeshKeys}");        // 
                }
            }
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace($"  Mesh Morph Channels: {anim.MeshMorphAnimationChannelCount} ");
            }

            foreach (var chan in anim.MeshMorphAnimationChannels)
            {
                if (StartupConsoleinfo && (TargetNodeConsoleName != "" || TargetNodeConsoleName == chan.Name))
                {
                    LogManager.Instance.WriteLineTrace($"  Channel {chan.Name}");
                    LogManager.Instance.WriteLineTrace($"    HasMeshMorphKeys: {chan.HasMeshMorphKeys}");       // 
                    LogManager.Instance.WriteLineTrace($"     MeshMorphKeyCount: {chan.MeshMorphKeyCount}");       // 
                                                                                                                   //LogManager.Instance.WriteLineTrace($"    Scaling  Keys: {chan.MeshMorphKeys}");        // 
                }
            }
            if (StartupConsoleinfo)
            {
                if (StartupConsoleinfo)
                {
                    LogManager.Instance.WriteLineTrace($"  HasNodeAnimations: {anim.HasNodeAnimations} ");
                    LogManager.Instance.WriteLineTrace($"   Node Channels: {anim.NodeAnimationChannelCount}");
                }
            }
            foreach (var chan in anim.NodeAnimationChannels)
            {
                if (StartupConsoleinfo && (TargetNodeConsoleName == "" || TargetNodeConsoleName == chan.NodeName))
                {
                    Console.Write($"   Channel {chan.NodeName}".PadRight(35));        // the node name has to be used to tie this channel to the originally printed hierarchy.  BTW, node names must be unique.
                    Console.Write($"     Position Keys: {chan.PositionKeyCount}".PadRight(25));         // access via chan.PositionKeys
                    Console.Write($"     Rotation Keys: {chan.RotationKeyCount}".PadRight(25));      // 
                    LogManager.Instance.WriteLineTrace($"     Scaling  Keys: {chan.ScalingKeyCount}".PadRight(25));        // 
                }
            }
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace("\n");
                LogManager.Instance.WriteLineTrace("\n Ok so this is all gonna go into our model class basically as is kinda. frownzers i needed it like this after all.");
            }
            foreach (var anode in anim.NodeAnimationChannels)
            {
                if (StartupConsoleinfo && (TargetNodeConsoleName == "" || TargetNodeConsoleName == anode.NodeName))
                {
                    LogManager.Instance.WriteLineTrace($"   Channel {anode.NodeName}\n   (time is in animation ticks it shouldn't exceed anim.DurationInTicks {anim.DurationInTicks} or total duration in seconds: {anim.DurationInTicks / anim.TicksPerSecond})");        // the node name has to be used to tie this channel to the originally printed hierarchy.  node names must be unique.
                    LogManager.Instance.WriteLineTrace($"     Position Keys: {anode.PositionKeyCount}");       // access via chan.PositionKeys

                    for (int j = 0; j < anode.PositionKeys.Count; j++)
                    {
                        var key = anode.PositionKeys[j];
                        if (StartupConsoleinfo)
                        {
                            LogManager.Instance.WriteLineTrace("       index[" + (j + "]").PadRight(5) + " Time: " + key.Time.ToString().PadRight(17) + " secs: " + (key.Time / anim.TicksPerSecond).ToStringTrimed() + "  Position: {" + key.Value.ToStringTrimed() + "}");
                        }
                    }
                    if (StartupConsoleinfo)
                    {
                        LogManager.Instance.WriteLineTrace($"     Rotation Keys: {anode.RotationKeyCount}");       // 
                    }

                    for (int j = 0; j < anode.RotationKeys.Count; j++)
                    {
                        var key = anode.RotationKeys[j];
                        if (StartupConsoleinfo)
                        {
                            LogManager.Instance.WriteLineTrace("       index[" + (j + "]").PadRight(5) + " Time: " + key.Time.ToStringTrimed() + " secs: " + (key.Time / anim.TicksPerSecond).ToStringTrimed() + "  QRotation: {" + key.Value.ToStringTrimed() + "}");
                        }
                    }
                    if (StartupConsoleinfo)
                    {
                        LogManager.Instance.WriteLineTrace($"     Scaling  Keys: {anode.ScalingKeyCount}");        // 
                    }

                    for (int j = 0; j < anode.ScalingKeys.Count; j++)
                    {
                        var key = anode.ScalingKeys[j];
                        if (StartupConsoleinfo)
                        {
                            LogManager.Instance.WriteLineTrace("       index[" + (j + "]").PadRight(5) + " Time: " + key.Time.ToStringTrimed() + " secs: " + (key.Time / anim.TicksPerSecond).ToStringTrimed() + "  Scaling: {" + key.Value.ToStringTrimed() + "}");
                        }
                    }
                }
            }
        }
    }



    /*
   */

    //=============================================================================
    /// <summary> Can be removed later or disregarded this is mainly for debuging. </summary>
    public void InfoFlatBones(RiggedModel model)
    {
        // just print out the flat node bones before we start so i can see whats up.
        if (StartupConsoleinfo)
        {
            LogManager.Instance.WriteLineTrace("");
            LogManager.Instance.WriteLineTrace("Flat bone nodes count: " + model.FlatListToBoneNodes.Count());
            for (int i = 0; i < model.FlatListToBoneNodes.Count(); i++)
            {
                var b = model.FlatListToBoneNodes[i];
                LogManager.Instance.WriteLineTrace(b.Name);
            }
            LogManager.Instance.WriteLineTrace("");
        }
    }

    //=============================================================================
    /// <summary> Can be removed later or disregarded this is mainly for debuging. </summary>
    public void InfoForMaterials(RiggedModel model, Scene scene)
    {
        for (int mloop = 0; mloop < scene.Meshes.Count; mloop++)
        {
            Mesh mesh = scene.Meshes[mloop];

            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace(
                "\n" + "__________________________" +
                "\n" + "Scene.Meshes[" + mloop + "] " +
                "\n" + "Mesh.Name: " + mesh.Name +
                "\n" + " FaceCount: " + mesh.FaceCount +
                "\n" + " VertexCount: " + mesh.VertexCount +
                "\n" + " Normals.Count: " + mesh.Normals.Count +
                "\n" + " BoneCount: " + mesh.BoneCount +
                "\n" + " MaterialIndex: " + mesh.MaterialIndex
                );
                LogManager.Instance.WriteLineTrace("  mesh.UVComponentCount.Length: " + mesh.UVComponentCount.Length);
            }
            for (int i = 0; i < mesh.UVComponentCount.Length; i++)
            {
                int val = mesh.UVComponentCount[i];
                if (StartupConsoleinfo && val > 0)
                {
                    LogManager.Instance.WriteLineTrace("     mesh.UVComponentCount[" + i + "] : int value: " + val);
                }
            }
            var tcc = mesh.TextureCoordinateChannelCount;
            var tc = mesh.TextureCoordinateChannels;
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace("  mesh.HasMeshAnimationAttachments: " + mesh.HasMeshAnimationAttachments);
                LogManager.Instance.WriteLineTrace("  mesh.TextureCoordinateChannelCount: " + mesh.TextureCoordinateChannelCount);
                LogManager.Instance.WriteLineTrace("  mesh.TextureCoordinateChannels.Length:" + mesh.TextureCoordinateChannels.Length);
            }
            for (int i = 0; i < mesh.TextureCoordinateChannels.Length; i++)
            {
                var channel = mesh.TextureCoordinateChannels[i];
                if (StartupConsoleinfo && channel.Count > 0)
                {
                    LogManager.Instance.WriteLineTrace("     mesh.TextureCoordinateChannels[" + i + "]  count " + channel.Count);
                }

                for (int j = 0; j < channel.Count; j++)
                {
                    // holds uvs and shit i think
                    //Console.GenerateClassCode(" channel[" + j + "].Count: " + channel.Count);
                }
            }
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace("");
            }

            //// Uv
            //LogManager.Instance.WriteLineTrace("");
            //var uvchannels = mesh.TextureCoordinateChannels;
            //for (int k = 0; k < uvchannels.Length; k++)
            //{
            //    var f = uvchannels[k];
            //    int loopIndex = 0;
            //    for (int j = 0; j < f.Count; j++)
            //    {
            //        var uv = f[j];
            //        v[loopIndex].TextureCoordinate = new Microsoft.Xna.Framework.Vector2(uv.X, uv.Y);
            //        loopIndex++;
            //    }
            //}
        }


        if (scene.HasTextures)
        {
            var texturescount = scene.TextureCount;
            var textures = scene.Textures;
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace("\nTextures " + " Count " + texturescount + "\n");
            }

            for (int i = 0; i < textures.Count; i++)
            {
                var name = textures[i];
                if (StartupConsoleinfo)
                {
                    LogManager.Instance.WriteLineTrace("Textures[" + i + "] " + name);
                }
            }
        }
        else
        {
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace("\nTextures " + " None ");
            }
        }

        if (scene.HasMaterials)
        {
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace("\nMaterials scene.MaterialCount " + scene.MaterialCount + "\n");
            }

            for (int i = 0; i < scene.Materials.Count; i++)
            {
                if (StartupConsoleinfo)
                {
                    LogManager.Instance.WriteLineTrace("");
                    LogManager.Instance.WriteLineTrace("Material[" + i + "] ");
                    LogManager.Instance.WriteLineTrace("Material[" + i + "].Name " + scene.Materials[i].Name);
                }
                var m = scene.Materials[i];
                if (m.HasName)
                {
                    if (StartupConsoleinfo)
                    {
                        Console.Write(" Name: " + m.Name);
                    }
                }
                var t = m.GetAllMaterialTextures();
                if (StartupConsoleinfo)
                {
                    LogManager.Instance.WriteLineTrace("  GetAllMaterialTextures Length " + t.Length);
                    LogManager.Instance.WriteLineTrace("");
                }
                for (int j = 0; j < t.Length; j++)
                {
                    var tindex = t[j].TextureIndex;
                    var toperation = t[j].Operation;
                    var ttype = t[j].TextureType.ToString();
                    var tfilepath = t[j].FilePath;
                    // J matches up to the texture coordinate channel uv count it looks like.
                    if (StartupConsoleinfo)
                    {
                        LogManager.Instance.WriteLineTrace("   Texture[" + j + "] " + "   Index:" + tindex + "   Type: " + ttype + "   Filepath: " + tfilepath);
                    }
                }
                if (StartupConsoleinfo)
                {
                    LogManager.Instance.WriteLineTrace("");
                }

                // added info
                if (StartupConsoleinfo)
                {
                    LogManager.Instance.WriteLineTrace("   Material[" + i + "] " + "  HasBlendMode:" + m.HasBlendMode + "  HasBumpScaling: " + m.HasBumpScaling + "  HasOpacity: " + m.HasOpacity + "  HasShadingMode: " + m.HasShadingMode + "  HasTwoSided: " + m.HasTwoSided + "  IsTwoSided: " + m.IsTwoSided);
                    LogManager.Instance.WriteLineTrace("   Material[" + i + "] " + "  HasBlendMode:" + m.HasShininess + "  HasTextureDisplacement:" + m.HasTextureDisplacement + "  HasTextureEmissive:" + m.HasTextureEmissive + "  HasTextureReflection:" + m.HasTextureReflection);
                    LogManager.Instance.WriteLineTrace("   Material[" + i + "] " + "  HasTextureReflection " + scene.Materials[i].HasTextureReflection + "  HasTextureLightMap " + scene.Materials[i].HasTextureLightMap + "  Reflectivity " + scene.Materials[i].Reflectivity);
                    LogManager.Instance.WriteLineTrace("   Material[" + i + "] " + "  ColorAmbient:" + m.ColorAmbient + "  ColorDiffuse: " + m.ColorDiffuse + "  ColorSpecular: " + m.ColorSpecular);
                    LogManager.Instance.WriteLineTrace("   Material[" + i + "] " + "  ColorReflective:" + m.ColorReflective + "  ColorEmissive: " + m.ColorEmissive + "  ColorTransparent: " + m.ColorTransparent);
                }
            }
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace("");
            }
        }
        else
        {
            if (StartupConsoleinfo)
            {
                LogManager.Instance.WriteLineTrace("\n   No Materials Present. \n");
            }
        }
    }

}

public class TempWeightVert
{
    public int CountOfBoneEntrysForThisVertice = 0;
    public readonly List<float> VerticesFlatBoneId = new();
    public readonly List<int> VerticeIndexs = new();
    public readonly List<float> VerticeBoneWeights = new();
}