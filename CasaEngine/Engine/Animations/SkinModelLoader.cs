//#define USING_COLORED_VERTICES  // uncomment this in both SkinModel and SkinModelLoader if using
using Assimp;
using Assimp.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using CasaEngine.Core.Logger;
using CasaEngine.Engine.Animations.SkinModelHelpers;

namespace CasaEngine.Engine.Animations;

public class SkinModelLoader
{
    // DEBUG DISPLAY SETTINGS
    public bool FilePathDebug = false;
    public bool LoadedModelInfo = false; //
    public bool AssimpInfo = false;
    public bool MinimalInfo = true;  // 
    public bool ConsoleInfo = false; // from here down is mostly run time console info.
    public bool MatrixInfo = false;
    public bool MeshBoneCreationInfo = false;
    public bool MaterialInfo = false;
    public bool FlatBoneInfo = false;
    public bool NodeTreeInfo = false;
    public bool AnimationInfo = false;
    public bool AnimationKeysInfo = false;
    public string targetNodeName = "";

    GraphicsDevice _graphicsDevice;
    public Scene _scene;
    public string FilePathName;
    public string FilePathNameWithoutExtension;
    public string AltDirectory;
    public static ContentManager Content;
    public static bool use_debug_tex;
    public static Texture2D debug_tex;
    public LoadDebugInfo logger;

    // LoadingLevelPreset: 
    // 0 = TargetRealTimeMaximumQuality, 1 = TargetRealTimeQuality, 2 = TargetRealTimeFast, 3 = custom (does it's best to squash meshes down - good for some older models)
    // ReverseVertexWinding:
    // Reverses the models winding - typically this will change the model vertices to counter clockwise winding (CCW).
    // AddLoopingDuration:
    // Artificially adds a small amount of looping duration to the end of a animation.This helps to fix animations that aren't properly looped.
    // Turn on AddAdditionalLoopingTime to use this.
    // Configuration stuff: 
    // https://github.com/assimp/assimp-net/blob/master/AssimpNet/Configs/PropertyConfig.cs
    public static int LoadingLevelPreset = 3;
    public bool ReverseVerticeWinding = false;
    public float AddedLoopingDuration = 0f;

    public List<PropertyConfig> configurations = new()
    {
        new NoSkeletonMeshesConfig(true),      // true to disable dummy-skeleton mesh
        new FBXImportCamerasConfig(false),     // true would import cameras
        new SortByPrimitiveTypeConfig(Assimp.PrimitiveType.Point | Assimp.PrimitiveType.Line), // primitive types we should remove
        new VertexBoneWeightLimitConfig(4),    // max weights per vertex (4 is very common - our shader will use 4)
        new NormalSmoothingAngleConfig(66.0f), // if no normals, generate (threshold 66 degrees) 
        new FBXStrictModeConfig(false),        // true only for fbx-strict-mode
    };

    public SkinModelLoader(ContentManager content, GraphicsDevice graphicsDevice)
    {
        Content = content;
        _graphicsDevice = graphicsDevice;
        logger = new LoadDebugInfo();
    }

    public void SetDefaultOptions(float AddToLoopDuration, Texture2D debugTexture)
    {
        AddedLoopingDuration = AddToLoopDuration;
        debug_tex = debugTexture;
        use_debug_tex = debugTexture != null;
    }

    public SkinModel Load(string filePath_or_fileName, string altTextureDirectory, SkinFx skin_fx, float rescale = 1f)
    {
        AltDirectory = altTextureDirectory;
        return Load(filePath_or_fileName, skin_fx, rescale);
    }

    public SkinModel Load(string filePath_or_fileName, string altTextureDirectory, bool useDebugTexture, SkinFx skin_fx, float rescale = 1f)
    {
        use_debug_tex = useDebugTexture;
        AltDirectory = altTextureDirectory;
        return Load(filePath_or_fileName, skin_fx, rescale);
    }

    public SkinModel Load(string filePath_or_fileName, string altTextureDirectory, bool useDebugTexture, int loadingLevelPreset, SkinFx skin_fx, float rescale = 1f)
    {
        LoadingLevelPreset = loadingLevelPreset;
        use_debug_tex = useDebugTexture;
        AltDirectory = altTextureDirectory;
        return Load(filePath_or_fileName, skin_fx, rescale);
    }

    public SkinModel Load(string filePath_or_fileName, SkinFx skin_fx, float rescale = 1f)
    {
        FilePathName = filePath_or_fileName;
        FilePathNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath_or_fileName);
        string s = Path.Combine(Environment.CurrentDirectory, filePath_or_fileName); // rem: set FBX to "copy-to" and remove any file processing properties
        if (File.Exists(s) == false)
        {
            if (FilePathDebug) { LogManager.Instance.WriteLineDebug("(not found) Checked for: " + s); }
            s = Path.Combine(Path.Combine(Environment.CurrentDirectory, "Assets"), filePath_or_fileName); // If file is placed in a project assets folder (with copy-to property set)            
        }
        if (File.Exists(s) == false)
        {
            if (FilePathDebug) { LogManager.Instance.WriteLineDebug("(not found) Instead tried to find: " + s); }
            s = Path.Combine(Environment.CurrentDirectory, filePath_or_fileName); // maybe in the exe directory
        }
        if (File.Exists(s) == false)
        {
            if (FilePathDebug) { LogManager.Instance.WriteLineDebug("(not found) Instead tried to find: " + s); }
            s = filePath_or_fileName;                    // maybe the exact complete path is specified
        }
        if (FilePathDebug) { LogManager.Instance.WriteLineDebug("Final file/path checked for: " + s); }
        Debug.Assert(File.Exists(s), "Could not find the file to load: " + s);
        string fullFilePath = s;

        // SETUP ASSIMP IMPORTER AND CONFIGURATIONS
        var importer = new AssimpContext();
        foreach (var p in configurations)
        {
            importer.SetConfig(p);
        }

        importer.Scale = rescale;

        // LOAD FILE INTO "SCENE" (an assimp imported model is in a thing called Scene)
        try
        {
            switch (LoadingLevelPreset)
            {
                case 0:
                    _scene = importer.ImportFile(fullFilePath, PostProcessPreset.TargetRealTimeMaximumQuality);
                    break;
                case 1:
                    _scene = importer.ImportFile(fullFilePath, PostProcessPreset.TargetRealTimeQuality);
                    break;
                case 2:
                    _scene = importer.ImportFile(fullFilePath, PostProcessPreset.TargetRealTimeFast);
                    break;
                default:
                    _scene = importer.ImportFile(fullFilePath,
                        PostProcessSteps.FlipUVs // currently need
                        | PostProcessSteps.JoinIdenticalVertices // optimizes indexed
                        | PostProcessSteps.Triangulate // precaution
                        | PostProcessSteps.FindInvalidData // sometimes normals export wrong (remove & replace:)
                        | PostProcessSteps
                            .GenerateSmoothNormals // smooths normals after identical verts removed (or bad normals)
                        | PostProcessSteps
                            .ImproveCacheLocality // possible better cache optimization                                        
                        | PostProcessSteps
                            .FixInFacingNormals // doesn't work well with planes - turn off if some faces go dark                                       
                        | PostProcessSteps.CalculateTangentSpace // use if you'll probably be using normal mapping 
                        | PostProcessSteps
                            .GenerateUVCoords // useful for non-uv-map export primitives                                                
                        | PostProcessSteps.ValidateDataStructure
                        | PostProcessSteps.FindInstances
                        | PostProcessSteps
                            .GlobalScale // use with AI_CONFIG_GLOBAL_SCALE_FACTOR_KEY (if need)                                                
                        | PostProcessSteps
                            .FlipWindingOrder // (CCW to CW) Depends on your rasterizing setup (need clockwise to fix inside-out problem?)                                                 
                                              //| PostProcessSteps.RemoveRedundantMaterials // use if not using material names to ID special properties                                                
                                              //| PostProcessSteps.FindDegenerates      // maybe (if using with AI_CONFIG_PP_SBP_REMOVE to remove points/lines)
                                              //| PostProcessSteps.SortByPrimitiveType  // maybe not needed (sort points, lines, etc)
                                              //| PostProcessSteps.OptimizeMeshes       // not suggested for animated stuff
                                              //| PostProcessSteps.OptimizeGraph        // not suggested for animated stuff                                        
                                              //| PostProcessSteps.TransformUVCoords    // maybe useful for keyed uv transforms                                                
                    );
                    break;
            }
        }
        catch (AssimpException ex)
        {
            throw new Exception("Problem reading file: " + fullFilePath + " (" + ex.Message + ")");
        }

        return CreateModel(fullFilePath, skin_fx);
    }

    private SkinModel CreateModel(string file_nom, SkinFx skinFx)
    {
        SkinModel model = new SkinModel(_graphicsDevice, skinFx); // create model
        model.debug_tex = debug_tex;
        model.use_debug_tex = use_debug_tex;

        CreateRootNode(model, _scene);                     // prep to build model's tree (need root node)

        CreateMeshesAndBones(model, _scene, 0);            // create the model's meshes and bones

        SetupMaterialsAndTextures(model, _scene);          // setup the materials and textures of each mesh

        // recursively search and add the nodes for our model from "scene", this includes adding to the flat bone & node lists
        CreateTreeTransforms(model, model.rootNodeOfTree, _scene.RootNode, 0);

        PrepareAnimationsData(model, _scene);             // get the animations in the file into each node's animations framelist           

        CopyVertexIndexData(model, _scene);               // get the vertices from the meshes  

        logger.AssimpSceneDump(this, _scene);                     // can dump scene data if need to            
        logger.DisplayInfo(this, model, file_nom);               // more info

        return model;
    }

    private void CreateRootNode(SkinModel model, Scene scene)
    {
        model.rootNodeOfTree = new SkinModel.ModelNode();
        model.rootNodeOfTree.name = scene.RootNode.Name;        // set the rootnode
        // set the rootnode transforms
        model.rootNodeOfTree.local_mtx = scene.RootNode.Transform.ToMgTransposed();  // ToMg converts to monogame compatible version
        model.rootNodeOfTree.combined_mtx = model.rootNodeOfTree.local_mtx;

        if (MaterialInfo)
        {
            logger.CreatingRootInfo(" Creating root node,  scene.RootNode.Name: " + scene.RootNode.Name + "   scene.RootNode.MeshCount: " + scene.RootNode.MeshCount + "   scene.RootNode.ChildCount: " + scene.RootNode.ChildCount);
            if (MatrixInfo)
            {
                LogManager.Instance.WriteLineDebug(" scene.RootNode.Transform.ToMgTransposed() " + scene.RootNode.Transform.ToMgTransposed());
            }
        }
    }

    /// <summary> We create model mesh instances for each mesh in scene.meshes. This is just set up here - it doesn't load any data. </summary>
    private void CreateMeshesAndBones(SkinModel model, Scene scene, int meshIndex)
    {
        if (MeshBoneCreationInfo)
        {
            LogManager.Instance.WriteLineDebug("\n\n@@@CreateModelMeshesAndBones \n");
        }

        model.meshes = new SkinModel.SkinMesh[scene.Meshes.Count];   // allocate skin meshes array

        // create the meshes (from "scene")
        for (int mi = 0; mi < scene.Meshes.Count; mi++)
        {
            Mesh assimpMesh = scene.Meshes[mi];
            var sMesh = new SkinModel.SkinMesh();              // make new SkinMesh
            sMesh.Name = assimpMesh.Name;                       // name
            sMesh.meshNumber = mi;                                    // index from scene                
            sMesh.tex_name = "Default";                             // texture name
            sMesh.material_index = assimpMesh.MaterialIndex;           // index of material used
            sMesh.material_name = scene.Materials[sMesh.material_index].Name; // material name

            var assimpMeshBones = assimpMesh.Bones;
            sMesh.hasBones = assimpMesh.HasBones;                   // has bones? 
            sMesh.shader_matrices = new Matrix[assimpMeshBones.Count + 1]; // allocate enough shader matrices
            sMesh.shader_matrices[0] = Matrix.Identity;                       // default=identity; if no bone/node animation, this'll keep it static for the duration
            sMesh.meshBones = new SkinModel.ModelBone[assimpMesh.BoneCount + 1]; // allocate bones

            // DUMMY BONE: Can't yet link this to the node - that must wait - it's not yet created & only exists in the model (not in the assimp bone list)
            sMesh.meshBones[0] = new SkinModel.ModelBone();        // make dummy ModelBone                
            var flatBone = sMesh.meshBones[0];               // reference the bone
            flatBone.offset_mtx = Matrix.Identity;
            flatBone.name = assimpMesh.Name;                  // "DummyBone0";
            flatBone.meshIndex = mi;                               // index of the mesh this bone belongs to
            flatBone.boneIndex = 0;                                // (note that since we're making a dummy bone at index 0, we'll add 1 to the others)                

            // CREATE/ADD BONES (from assimp data):
            for (int abi = 0; abi < assimpMeshBones.Count; abi++)
            {      // loop thru bones
                var assimpBone = assimpMeshBones[abi];               // refer to bone                    

                var boneIndex = abi + 1;                            // add 1 because we made a dummy bone
                sMesh.shader_matrices[boneIndex] = Matrix.Identity;      // init shader matrices
                sMesh.meshBones[boneIndex] = new SkinModel.ModelBone();  // make ModelBone
                flatBone = sMesh.meshBones[boneIndex]; // refer to the new bone
                flatBone.name = assimpBone.Name;            // name it
                flatBone.offset_mtx = assimpBone.OffsetMatrix.ToMgTransposed(); // set the offset matrix (compatible version)
                flatBone.meshIndex = mi;                         // assign the associated mesh index
                flatBone.boneIndex = boneIndex;                  // index of the bone
                flatBone.numWeightedVerts = assimpBone.VertexWeightCount; // how many vertex weights? 
                sMesh.meshBones[boneIndex] = flatBone;                   // put the new bone into the bone list
            }
            model.meshes[mi] = sMesh;        // add the new SkinMesh to the mesh list

            // SHOW DEBUG INFO (if set to)
            if (MeshBoneCreationInfo)
            {
                logger.ShowMeshBoneCreationInfo(assimpMesh, sMesh, MatrixInfo, mi);
            }
        }
    }


    //-------------------------------------------------------------
    //-------------------------------------------------------------
    /// <summary> Loads textures and sets material values to each model mesh. </summary>
    private void SetupMaterialsAndTextures(SkinModel model, Scene scene)
    {
        if (MaterialInfo)
        {
            LogManager.Instance.WriteLineDebug("\n\n@@@SetUpMeshMaterialsAndTextures \n");
        }

        var savedDir = Content.RootDirectory;                            // store the current Content directory to restore it later

        for (int mi = 0; mi < scene.Meshes.Count; mi++)
        {                // loop thru scene meshes
            var sMesh = model.meshes[mi];                             // ref to model-mesh
            int matIndex = sMesh.material_index;                         // we need the material index (for scene)
            var assimpMaterial = scene.Materials[matIndex];              // get the scene's material 

            sMesh.ambient = assimpMaterial.ColorAmbient.ToMg();    // minimum light color
            sMesh.diffuse = assimpMaterial.ColorDiffuse.ToMg();    // regular material colorization
            sMesh.specular = assimpMaterial.ColorSpecular.ToMg();   // specular highlight color 
            sMesh.emissive = assimpMaterial.ColorEmissive.ToMg();   // amplify a color brightness (not requiring light - similar to ambient really - kind of a glow without light)                 
            sMesh.opacity = assimpMaterial.Opacity;                // how opaque or see-through is it? 
            sMesh.reflectivity = assimpMaterial.Reflectivity;           // strength of reflections
            sMesh.shininess = assimpMaterial.Shininess;              // how much light shines off
            sMesh.shineStrength = assimpMaterial.ShininessStrength;      // probably specular power (can use to narrow & intensifies highlights - ie: more wet or metallic looking)
            sMesh.bumpScale = assimpMaterial.BumpScaling;            // amplify or reduce normal-map effect
            sMesh.isTwoSided = assimpMaterial.IsTwoSided;             // can be useful for glass or ice
            //sMesh.colorTransparent   = assimpMaterial.ColorTransparent.ToMg();
            //sMesh.reflective         = assimpMaterial.ColorReflective.ToMg(); 
            //sMesh.transparency       = assimpMaterial.TransparencyFactor;
            //sMesh.hasShaders         = assimpMaterial.HasShaders;
            //sMesh.shadingMode        = assimpMaterial.ShadingMode.ToString();
            //sMesh.blendMode          = assimpMaterial.BlendMode.ToString();
            //sMesh.isPbrMaterial      = assimpMaterial.IsPBRMaterial;
            //sMesh.isWireFrameEnabled = assimpMaterial.IsWireFrameEnabled;

            var assimpMaterialTextures = assimpMaterial.GetAllMaterialTextures();  // get the list of textures
            if (MaterialInfo) { LogManager.Instance.WriteLineDebug("\n Mesh Name " + scene.Meshes[mi].Name); LogManager.Instance.WriteLineDebug(" MaterialIndexName: " + sMesh.material_name + "  Materials[" + matIndex + "]   get material textures"); }
            // Texture types available via assimp are:
            // None = 0, Diffuse = 1,Specular = 2, Ambient = 3,Emissive = 4, Height = 5,Normals = 6,Shininess = 7,Opacity = 8, Displacement = 9, Lightmap = 10,AmbientOcclusion = 17,Reflection = 11,
            // BaseColor = 12 /*PBR*/, NormalCamera = 13/*PBR normal map workflow*/,EmissionColor = 14/*PBR emissive*/,Metalness = 15 /*PBR shininess*/  , Roughness = 16, /*PRB*/

            for (int t = 0; t < assimpMaterialTextures.Length; t++)
            {              // loop thru textures
                var tindex = assimpMaterialTextures[t].TextureIndex;           // texture index
                var toperation = assimpMaterialTextures[t].Operation;
                var ttype = assimpMaterialTextures[t].TextureType.ToString(); // texture type
                var tfilepath = assimpMaterialTextures[t].FilePath;               // original file path I think
                var tfilename = GetFileName(tfilepath, true);                     // just file's name                    

                // PREPARE CORRECT LOADING DIRECTORY (if texture file can be found)
                var tfullfilepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Content.RootDirectory, tfilename + ".xnb"); // expected file
                var tfileexists = File.Exists(tfullfilepath);   // is there an xnb? 
                if (tfileexists == false)
                {
                    tfullfilepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Content.RootDirectory, FilePathNameWithoutExtension, tfilename + ".xnb");
                    tfileexists = File.Exists(tfullfilepath);   // how about this one? 
                    if (tfileexists == true) { Content.RootDirectory = Path.Combine("Content", FilePathNameWithoutExtension); }    // switch Content's load directory
                    else
                    {
                        tfullfilepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Content.RootDirectory, AltDirectory, tfilename + ".xnb"); // in alternative directory?
                        tfileexists = File.Exists(tfullfilepath);
                        if (tfileexists == true) { Content.RootDirectory = Path.Combine("Content", AltDirectory); }                // switch Content's load directory 
                    }
                }
                // debug info if needed: 
                if (MaterialInfo)
                {
                    LogManager.Instance.WriteLineDebug("      Material[" + matIndex + "].Texture[" + t + "] of " + assimpMaterialTextures.Length + "   Index: " + tindex.ToString().PadRight(5) + "   Type: " + ttype.PadRight(15) + "   Operation: " + toperation.ToString().PadRight(15) + " Name: " + tfilename.PadRight(15) + "  ExistsInContent: " + tfileexists); LogManager.Instance.WriteLineDebug("      Filepath: " + tfilepath.PadRight(15));
                }
                // L O A D   T E X T U R E S
                if (ttype == "Diffuse")
                {
                    if (MaterialInfo)
                    {
                        LogManager.Instance.WriteLineDebug("      ....Type...Diffuse");
                    }

                    model.meshes[mi].tex_name = tfilename;   // Get diffuse tex name
                    if (Content != null && tfileexists)
                    {    // If exists, load it: 
                        model.meshes[mi].tex_diffuse = Content.Load<Texture2D>(tfilename); if (MaterialInfo)
                        {
                            LogManager.Instance.WriteLineDebug("      ....Type...Diffuse Texture loaded: ... " + tfilename);
                        }
                    }
                    else
                    {
                        LogManager.Instance.WriteLineDebug("      DID NOT LOAD....: " + tfilename);
                    }
                }
                if (ttype == "Normals")
                {
                    if (MaterialInfo)
                    {
                        LogManager.Instance.WriteLineDebug("      ....Type...Normals");
                    }

                    model.meshes[mi].tex_normMap_name = tfilename; // Get normalMap tex name
                    if (Content != null && tfileexists)
                    {          // If exists, load it:
                        model.meshes[mi].tex_normalMap = Content.Load<Texture2D>(tfilename); if (MaterialInfo)
                        {
                            LogManager.Instance.WriteLineDebug("      ....Type...Normal Map Texture loaded: ... " + tfilename);
                        }
                    }
                    else
                    {
                        LogManager.Instance.WriteLineDebug("      DID NOT LOAD....: " + tfilename);
                    }
                }
                if (ttype == "Specular")
                {
                    if (MaterialInfo)
                    {
                        LogManager.Instance.WriteLineDebug("      ....Type...Specular");
                    }

                    model.meshes[mi].tex_specular_name = tfilename; // Get specularMap tex name
                    if (Content != null && tfileexists)
                    {           // If exists, load it:
                        model.meshes[mi].tex_specular = Content.Load<Texture2D>(tfilename); if (MaterialInfo)
                        {
                            LogManager.Instance.WriteLineDebug("      ....Type...Specular Map Texture loaded: ... " + tfilename);
                        }
                    }
                    else
                    {
                        LogManager.Instance.WriteLineDebug("      DID NOT LOAD....: " + tfilename);
                    }
                }
                if (ttype == "Height")
                {
                    if (MaterialInfo)
                    {
                        LogManager.Instance.WriteLineDebug("      ....Type...Height");
                    }

                    model.meshes[mi].tex_heightMap_name = tfilename; // Get height tex name
                    if (Content != null && tfileexists)
                    {            // If exists, load it: 
                        model.meshes[mi].tex_heightMap = Content.Load<Texture2D>(tfilename); if (MaterialInfo)
                        {
                            LogManager.Instance.WriteLineDebug("      ....Type...Height Texture loaded: ... " + tfilename);
                        }
                    }
                    else
                    {
                        LogManager.Instance.WriteLineDebug("      DID NOT LOAD....: " + tfilename);
                    }
                }
                if (ttype == "Reflection")
                {
                    if (MaterialInfo)
                    {
                        LogManager.Instance.WriteLineDebug("      ....Type...Reflection");
                    }

                    model.meshes[mi].tex_reflectionMap_name = tfilename; // Get reflectionMap tex name
                    if (Content != null && tfileexists)
                    {                // If exists, load it:
                        model.meshes[mi].tex_reflectionMap = Content.Load<Texture2D>(tfilename); if (MaterialInfo)
                        {
                            LogManager.Instance.WriteLineDebug("      ....Type...Reflection Map Texture loaded: ... " + tfilename);
                        }
                    }
                    else
                    {
                        LogManager.Instance.WriteLineDebug("      DID NOT LOAD....: " + tfilename);
                    }
                }
            }
        }
        Content.RootDirectory = savedDir; // done loading - restore to original Content directory
    }


    //-------------------------------------------------
    //-------------------------------------------------
    /// <summary> Recursively get scene nodes stuff into our model nodes
    /// - get node name, init local matrix, assign non-bone mesh transforms to corresponding mesh
    /// - match up by name: meshes-index/bone-index with this bone (which meshes and mesh-bone-list-index(for unique mesh-relative offsets) this bone deals with)
    /// - create the children /// </summary>
    private void CreateTreeTransforms(SkinModel model, SkinModel.ModelNode modelNode, Node curAssimpNode, int tabLevel)
    {
        modelNode.name = curAssimpNode.Name;                           // get node name
        modelNode.local_mtx = curAssimpNode.Transform.ToMgTransposed();     // set initial local transform
        modelNode.combined_mtx = curAssimpNode.Transform.ToMgTransposed();

        // IF IS A MESH NODE: (instead of bone node)
        if (curAssimpNode.HasMeshes)
        {
            modelNode.isMeshNode = true;
            foreach (int meshIndex in curAssimpNode.MeshIndices)
            {      // loop through the list of mesh indices
                var sMesh = model.meshes[meshIndex];   // refer to the corresponding skinMesh
                sMesh.node_with_anim_trans = modelNode;                 // tell the mesh to get it's transform from the current node in the tree
            }
        }

        // For each assimpNode in the tree, we create a uniqueMeshBones list which holds information about which meshes are affected (and thus need index of bone for each mesh)
        // We can find the bone to use within each mesh's bone-list by finding a matching name. We store the applicable mesh#'s and bone#'s 
        // to be able to affect more than 1 mesh with a bone later when recursing the tree for animation updates. 
        for (int mi = 0; mi < _scene.Meshes.Count; mi++)
        {
            SkinModel.ModelBone bone;
            int boneIndexInMesh = 0;
            if (GetBoneForMesh(model.meshes[mi], modelNode.name, out bone, out boneIndexInMesh))
            { // find the bone that goes with this node name
                // MARK AS BONE: 
                modelNode.hasRealBone = true;      // yes, we found it in this mesh's bone-list
                modelNode.isBoneOnRoute = true;      // "                    
                bone.meshIndex = mi;                 // record index of every mesh the bone should affect
                bone.boneIndex = boneIndexInMesh;    // record index of the bone within each affected mesh's bone-list
                modelNode.uniqueMeshBones.Add(bone); // add the bone into our flat-list of bones (could be only 1 mesh, could be multiple)
            }
        }
        // debug info (if needed)
        if (NodeTreeInfo)
        {
            logger.ShowNodeTreeInfo(tabLevel, curAssimpNode, MatrixInfo, modelNode, model, _scene);
        }

        // CHILDREN: 
        for (int i = 0; i < curAssimpNode.Children.Count; i++)
        {
            var asimpChildNode = curAssimpNode.Children[i];
            var childNode = new SkinModel.ModelNode();       // made each child node                
            childNode.parent = modelNode;                       // set parent before passing
            childNode.name = curAssimpNode.Children[i].Name;  // name the child
            if (childNode.parent.isBoneOnRoute)
            {
                childNode.isBoneOnRoute = true; // part of actual tree
            }

            modelNode.children.Add(childNode);                    // add each child to this node's child list
            CreateTreeTransforms(model, modelNode.children[i], asimpChildNode, tabLevel + 1); // recursively create transforms for each child
        }
    }


    //---------------------------------------------------
    //---------------------------------------------------
    /// <summary> Gets the assimp animations into our model </summary>
    // http://sir-kimmi.de/assimp/lib_html/_animation_overview.html
    // http://sir-kimmi.de/assimp/lib_html/structai_animation.html
    // http://sir-kimmi.de/assimp/lib_html/structai_anim_mesh.html            
    private void PrepareAnimationsData(SkinModel model, Scene scene)
    {
        if (AnimationInfo)
        {
            LogManager.Instance.WriteLineDebug("\n\n@@@AnimationsCreateNodesAndCopy \n");
        }

        // Copy animation to ours
        for (int i = 0; i < scene.Animations.Count; i++)
        {
            var assimAnim = scene.Animations[i];
            if (AnimationInfo)
            {
                LogManager.Instance.WriteLineDebug("  " + "assimpAnim.Name: " + assimAnim.Name);
            }

            // Initially, copy data
            var modelAnim = new SkinModel.RigAnimation();
            modelAnim.animation_name = assimAnim.Name;
            modelAnim.TicksPerSecond = assimAnim.TicksPerSecond;
            modelAnim.DurationInTicks = assimAnim.DurationInTicks;
            modelAnim.DurationInSeconds = assimAnim.DurationInTicks / assimAnim.TicksPerSecond; // Time for entire animation
            modelAnim.DurationInSecondsAdded = AddedLoopingDuration;                            // May have added duration for animation-loop-fix                
            //modelAnim.TotalFrames  = (int)(modelAnim.DurationInSeconds * (double)(modelAnim.TicksPerSecond)); // just a default value
            modelAnim.HasNodeAnims = assimAnim.HasNodeAnimations;                               // maybe has no animation
            modelAnim.HasMeshAnims = assimAnim.HasMeshAnimations;                               // maybe has mesh transforms

            // Need an animation-node-list for each animation 
            modelAnim.animatedNodes = new List<SkinModel.AnimNodes>();         // lists of S,R,T keyframes for nodes
            // Loop the node channels
            for (int j = 0; j < assimAnim.NodeAnimationChannels.Count; j++)
            {
                var anodeAnimLists = assimAnim.NodeAnimationChannels[j];       // refer to assimp node animation lists (keyframes) for this channel
                var nodeAnim = new SkinModel.AnimNodes();                // make a new animNode [animation-list (keyframes)]
                nodeAnim.nodeName = anodeAnimLists.NodeName;                  // copy the animation name
                if (AnimationInfo)
                {
                    LogManager.Instance.WriteLineDebug("  " + " Animated Node Name: " + nodeAnim.nodeName);
                }

                // use name to get the node in our tree to refer to 
                var modelnoderef = GetRefToNode(anodeAnimLists.NodeName, model.rootNodeOfTree);
                if (modelnoderef == null)
                {
                    LogManager.Instance.WriteLineDebug("NODE SHOULD NOT BE NULL: " + anodeAnimLists.NodeName);
                }

                nodeAnim.nodeRef = modelnoderef; // set the bone this animation refers to

                // get the rotation, scale, and position keys: 
                foreach (var keyList in anodeAnimLists.RotationKeys)
                {
                    var oaq = keyList.Value;                                            // get open-assimp quaternion
                    nodeAnim.qrotTime.Add(keyList.Time / assimAnim.TicksPerSecond);     // add to list: rotation-time: time = keyTime / TicksPerSecond
                    nodeAnim.qrot.Add(oaq.ToMg());                                      // add to list: rotation (monogame compatible quaternion)
                }
                foreach (var keyList in anodeAnimLists.PositionKeys)
                {
                    var oap = keyList.Value.ToMg();                                     // get open-assimp position
                    nodeAnim.positionTime.Add(keyList.Time / assimAnim.TicksPerSecond); // add to list: position-time: time = keyTime / TicksPerSecond
                    nodeAnim.position.Add(oap);                                         // add to list: position
                }
                foreach (var keyList in anodeAnimLists.ScalingKeys)
                {
                    var oas = keyList.Value.ToMg();                                     // get open-assimp scale
                    nodeAnim.scaleTime.Add(keyList.Time / assimAnim.TicksPerSecond);    // add to list: scale-time: time = keyTime / TicksPerSecond
                    nodeAnim.scale.Add(oas);                                            // add to list: scale 
                }
                // Place this populated node into this model animation:
                modelAnim.animatedNodes.Add(nodeAnim);
            }
            // Place the animation into the model.
            model.Animations.Add(modelAnim);
        } // loop scene animations
    } // PrepareAnimationsData


    //-------------------------------------------------
    //-------------------------------------------------
    /// <summary> Copy data from scene to our meshes. </summary> // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e
    private void CopyVertexIndexData(SkinModel model, Scene scene)
    {
        if (ConsoleInfo)
        {
            LogManager.Instance.WriteLineDebug("\n\n@@@CopyVerticeIndiceData \n");
        }

        // LOOP SCENE MESHES for VERTEX DATA
        for (int mi = 0; mi < scene.Meshes.Count; mi++)
        {
            LogManager.Instance.WriteLineDebug($"Mesh {mi}");

            Mesh mesh = scene.Meshes[mi];
            if (ConsoleInfo)
            {
                logger.ShowMeshInfo(mesh, mi);
                for (int i = 0; i < mesh.UVComponentCount.Length; i++)
                {
                    int val = mesh.UVComponentCount[i];
                    LogManager.Instance.WriteLineDebug("" + " mesh.UVComponentCount[" + i + "] : " + val);
                }
                LogManager.Instance.WriteLineDebug(" Copying Indices...");
            }

            // INDICES
            int[] indices = new int[mesh.Faces.Count * 3]; // need 3 indices per face
            int numIndices = 0;
            for (int k = 0; k < mesh.Faces.Count; k++)
            {   // loop faces
                var f = mesh.Faces[k];                     // get face
                int indCount = f.IndexCount;
                if (indCount != 3)
                {
                    LogManager.Instance.WriteLineDebug("\n UNEXPECTED INDEX COUNT \n"); // may need to ensure load settings force triangulation
                }

                for (int j = 0; j < indCount; j++)
                {       // loop indices of face
                    var ind = f.Indices[j];                // get each index
                    indices[numIndices] = ind;             // store each index into big array of indices
                    numIndices++;                          // increment total number of indices
                }
            }

            // VERTICES
            if (ConsoleInfo)
            {
                LogManager.Instance.WriteLineDebug("" + " Copying Vertices...");
            }

            int numVerts = mesh.Vertices.Count;
            VertexNormMapSkin[] v = new VertexNormMapSkin[numVerts];        // allocate memory for vertex array
            for (int k = 0; k < numVerts; k++)
            {                            // loop vertices in mesh
                var f = mesh.Vertices[k];                                   // get vertex
                v[k].pos = new Vector3(f.X, f.Y, f.Z);                      // copy vertex position
            }

            // NORMALS
            if (ConsoleInfo)
            {
                LogManager.Instance.WriteLineDebug("" + " Copying Normals...");
            }

            for (int k = 0; k < mesh.Normals.Count; k++)
            {                  // loop normals
                var f = mesh.Normals[k];                                    // get normal    
                v[k].norm = new Vector3(f.X, f.Y, f.Z);                     // copy normal
            }

            // VERTEX COLORS
            // A mesh may contain 0 to AI_MAX_NUMBER_OF_COLOR_SETS vertex colors per vertex. NULL if not present. Each array is mNumVertices in size if present. 
            // http://sir-kimmi.de/assimp/lib_html/structai_mesh.html#aa2807c7ba172115203ed16047ad65f9e                
#if USING_COLORED_VERTICES
                if (ConsoleInfo) LogManager.Instance.WriteLineDebug("" + " Copying Colors...");
                var c = mesh.VertexColorChannels[mi];             // get color data for this mesh
                bool hascolors = false;
                if (mesh.HasVertexColors(mi)) hascolors = true;
                for (int k = 0; k < mesh.Vertices.Count; k++) {
                    if (hascolors == false) {
                        v[k].color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f); 
                    }
                    else {
                        v[k].color = new Vector4(c[k].R, c[k].G, c[k].B, c[k].A);
                    }
                }
#endif

            // UV's
            if (ConsoleInfo)
            {
                LogManager.Instance.WriteLineDebug("" + " Copying Uv TexCoords...");
            }

            var uvchannels = mesh.TextureCoordinateChannels;
            for (int k = 0; k < uvchannels.Length; k++)
            {// Loop UV channels
                var vertexUvCoords = uvchannels[k];
                int uvCount = 0;
                for (int j = 0; j < vertexUvCoords.Count; j++)
                {// Loop texture coords
                    var uv = vertexUvCoords[j];
                    v[uvCount].uv = new Vector2(uv.X, uv.Y);     // get the vertex's uv coordinate
                    uvCount++;
                }
            }

            /// We already set the Assimp to generate tangents & bitangents if needed (which needs normals &  uv's which we also told it to ensure), so this should work:  
            // TANGENTS
            //if (ConsoleInfo) LogManager.Instance.WriteLineDebug("" + " Copying Tangents...");
            //for (int k = 0; k < mesh.Tangents.Count; k++) { 
            //    var f = mesh.Tangents[k];
            //    v[k].tangent = new Vector3(f.X, f.Y, f.Z);                  // copy tangent                    
            //}                
            //// BI-TANGENTS
            //if (ConsoleInfo) LogManager.Instance.WriteLineDebug("" + " Copying BiTangents...");
            //for (int k = 0; k < mesh.BiTangents.Count; k++) {
            //    var f = mesh.BiTangents[k];
            //    v[k].biTangent = new Vector3(f.X, f.Y, f.Z);               // copy bi-tangent                    
            //}
            // REGENERATE TANGENTS AND BITANGENTS - LOADED ONES CAUSING NAN------------------------------------------------------------------
            var tan1 = new Vector3[numVerts];
            var tan2 = new Vector3[numVerts];

            for (var index = 0; index < numIndices; index += 3)
            {
                var i1 = indices[index + 0];
                var i2 = indices[index + 1];
                var i3 = indices[index + 2];

                var w1 = v[i1].uv;
                var w2 = v[i2].uv;
                var w3 = v[i3].uv;

                var s1 = w2.X - w1.X;
                var s2 = w3.X - w1.X;
                var t1 = w2.Y - w1.Y;
                var t2 = w3.Y - w1.Y;

                var denom = s1 * t2 - s2 * t1;
                if (Math.Abs(denom) < float.Epsilon)
                {
                    // The triangle UVs are zero sized one dimension. So we cannot calculate the 
                    // vertex tangents for this one trangle, but maybe it can with other trangles.
                    continue;
                }

                var r = 1.0f / denom;
                Debug.Assert(LoaderExtensions.IsFinite(r), "Bad r!");

                var v1 = v[i1].pos;
                var v2 = v[i2].pos;
                var v3 = v[i3].pos;

                var x1 = v2.X - v1.X;
                var x2 = v3.X - v1.X;
                var y1 = v2.Y - v1.Y;
                var y2 = v3.Y - v1.Y;
                var z1 = v2.Z - v1.Z;
                var z2 = v3.Z - v1.Z;

                var sdir = new Vector3()
                {
                    X = (t2 * x1 - t1 * x2) * r,
                    Y = (t2 * y1 - t1 * y2) * r,
                    Z = (t2 * z1 - t1 * z2) * r,
                };

                var tdir = new Vector3()
                {
                    X = (s1 * x2 - s2 * x1) * r,
                    Y = (s1 * y2 - s2 * y1) * r,
                    Z = (s1 * z2 - s2 * z1) * r,
                };

                tan1[i1] += sdir; Debug.Assert(tan1[i1].IsFinite(), "Bad tan1[i1]!");
                tan1[i2] += sdir; Debug.Assert(tan1[i2].IsFinite(), "Bad tan1[i2]!");
                tan1[i3] += sdir; Debug.Assert(tan1[i3].IsFinite(), "Bad tan1[i3]!");

                tan2[i1] += tdir; Debug.Assert(tan2[i1].IsFinite(), "Bad tan2[i1]!");
                tan2[i2] += tdir; Debug.Assert(tan2[i2].IsFinite(), "Bad tan2[i2]!");
                tan2[i3] += tdir; Debug.Assert(tan2[i3].IsFinite(), "Bad tan2[i3]!");
            }

            // At this point we have all the vectors accumulated, but we need to average
            // them all out. So we loop through all the final verts and do a Gram-Schmidt
            // orthonormalize, then make sure they're all unit length.
            for (var i = 0; i < numVerts; i++)
            {
                var n = v[i].norm;
                Debug.Assert(n.IsFinite(), "Bad normal!");
                Debug.Assert(n.Length() >= 0.9999f, "Bad normal!");

                var t = tan1[i];
                if (t.LengthSquared() < float.Epsilon)
                {
                    // TODO: Ideally we could spit out a warning to the content logging here!

                    // We couldn't find a good tanget for this vertex.                        
                    // Rather than set them to zero which could produce errors in other parts of 
                    // the pipeline, we just take a guess at something that may look ok.

                    t = Vector3.Cross(n, Vector3.UnitX);
                    if (t.LengthSquared() < float.Epsilon)
                    {
                        t = Vector3.Cross(n, Vector3.UnitY);
                    }

                    v[i].tangent = Vector3.Normalize(t);
                    v[i].biTangent = Vector3.Cross(n, v[i].tangent);
                    continue;
                }

                // Gram-Schmidt orthogonalize
                // TODO: This could be zero - could cause NaNs on normalize... how to fix this?
                var tangent = t - n * Vector3.Dot(n, t);
                tangent = Vector3.Normalize(tangent);
                Debug.Assert(tangent.IsFinite(), "Bad tangent!");
                v[i].tangent = tangent;

                // Calculate handedness
                var w = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0F) ? -1.0F : 1.0F;
                Debug.Assert(LoaderExtensions.IsFinite(w), "Bad handedness!");

                // Calculate the bitangent
                var bitangent = Vector3.Cross(n, tangent) * w;
                Debug.Assert(bitangent.IsFinite(), "Bad bitangent!");
                v[i].biTangent = bitangent;
            }
            //-------------------------------------------------------------------------------------------------------------------------------


            // GET BOUNDING BOX
            if (ConsoleInfo)
            {
                LogManager.Instance.WriteLineDebug("" + " Calculating min max centroid...");
            }

            Vector3 min = Vector3.Zero;
            Vector3 max = Vector3.Zero;
            Vector3 centroid = Vector3.Zero;
            foreach (var vert in v)
            {
                if (vert.pos.X < min.X) { min.X = vert.pos.X; }
                if (vert.pos.Y < min.Y) { min.Y = vert.pos.Y; }
                if (vert.pos.Z < min.Z) { min.Z = vert.pos.Z; }
                if (vert.pos.X > max.X) { max.X = vert.pos.X; }
                if (vert.pos.Y > max.Y) { max.Y = vert.pos.Y; }
                if (vert.pos.Z > max.Z) { max.Z = vert.pos.Z; }
                centroid += vert.pos;
            }
            model.meshes[mi].mid = centroid / (float)v.Length;
            model.meshes[mi].min = min;
            model.meshes[mi].max = max;

            // BLEND WEIGHTS AND BLEND INDICES
            if (ConsoleInfo)
            {
                LogManager.Instance.WriteLineDebug("" + " Copying and adjusting blend weights and indexs...");
            }

            for (int k = 0; k < mesh.Vertices.Count; k++) { v[k].blendIndices = Vector4.Zero; v[k].blendWeights = Vector4.Zero; }
            // Restructure vertex data to conform to a shader.
            // Iterate mesh bone offsets - set the bone Id's and weights to the vertices.
            // This also entails correlating the mesh local bone index names to the flat bone list.
            TempVertWeightIndices[] verts = new TempVertWeightIndices[mesh.Vertices.Count];
            if (mesh.HasBones)
            {
                model.meshes[mi].hasBones = mesh.HasBones;
                model.meshes[mi].hasMeshAnimAttachments = mesh.HasMeshAnimationAttachments;
                var assimpBones = mesh.Bones;                       // refer to current bone set in this mesh
                if (ConsoleInfo)
                {
                    LogManager.Instance.WriteLineDebug("   assimpMeshBones.Count: " + assimpBones.Count);
                }

                // LOOP: ASSIMP MESH BONES
                for (int ambi = 0; ambi < assimpBones.Count; ambi++)
                {                      // loop the bones of this assimp mesh
                    var assimBone = assimpBones[ambi];
                    var assimBoneName = assimpBones[ambi].Name;
                    var modelBoneIndex = ambi + 1;                                          // add 1 cuz we inserted a dummy at 0

                    // Debug Info (if needed) - could get the entire list of weights
                    if (ConsoleInfo)
                    {
                        string str = "     mesh[" + mi + "].Name: " + mesh.Name + "  bone[" + ambi + "].Name: " + assimBoneName.PadRight(12) + "  assimpMeshBoneIndex: " + ambi.ToString().PadRight(4) + "  WeightCount: " + assimBone.VertexWeightCount;
                        if (assimBone.VertexWeightCount > 0)
                        {
                            str += "  ex VertexWeights[0].VertexID: " + assimBone.VertexWeights[0].VertexID;
                        }

                        LogManager.Instance.WriteLineDebug(str);
                    }

                    // loop thru this bones vertex listings with the weights for it:
                    for (int wieght_index = 0; wieght_index < assimBone.VertexWeightCount; wieght_index++)
                    {
                        var vert_ind = assimBone.VertexWeights[wieght_index].VertexID;    // which vertex the bone-weight should be assigned to
                        var weight = assimBone.VertexWeights[wieght_index].Weight;      // get the weight
                        if (verts[vert_ind] == null)
                        {
                            verts[vert_ind] = new TempVertWeightIndices();                     // add new temp weight thing to store our info                             
                        }

                        verts[vert_ind].vertIndices.Add(vert_ind);                             // store vert index
                        verts[vert_ind].vertFlatBoneId.Add(modelBoneIndex);                    // store corrent index of bone
                        verts[vert_ind].vertBoneWeights.Add(weight);                           // store weight
                        verts[vert_ind].numBonesForThisVert++;                                 // store how many bones affect this vertex
                    }
                }
            }
            else
            {
                // If there is no bone data we will set it to bone zero. (basically a precaution - no bone data, no bones)
                // In this case, verts need to have a weight and be set to bone 0 (identity).
                // (allows us to draw a boneless mesh [as if entire mesh were attached to a single identity world bone])
                // If there is an actual world mesh node, we can combine the animated transform and set it to that bone as well.
                // So this will work for boneless node mesh transforms which assimp doesn't mark as a actual mesh transform when it is.
                for (int i = 0; i < verts.Length; i++)
                {
                    verts[i] = new TempVertWeightIndices();
                    var ve = verts[i];
                    if (ve.vertIndices.Count == 0)
                    {
                        // there is no bone data for this vertex, then we should set it to bone zero.
                        verts[i].vertIndices.Add(i);
                        verts[i].vertFlatBoneId.Add(0);
                        verts[i].vertBoneWeights.Add(1.0f);
                    }
                }
            }

            // Need up to 4 bone-influences per vertex - empties are 0, weight 0. We can add non-zero entries (0,1,2,3) based on how many were stored.
            // (Note: The bone weight data aligns with offset matrices bone names)                
            for (int i = 0; i < verts.Length; i++)
            {                       // loop the vertices
                if (verts[i] != null)
                {
                    var ve = verts[i];                                     // get vertex entry
                    //NOTE: maxbones = 4 
                    var arrayIndex = ve.vertIndices.ToArray();
                    var arrayBoneId = ve.vertFlatBoneId.ToArray();
                    var arrayWeight = ve.vertBoneWeights.ToArray();
                    if (arrayBoneId.Length > 3)
                    {
                        v[arrayIndex[3]].blendIndices.W = arrayBoneId[3];  // we can copy entry 4
                        v[arrayIndex[3]].blendWeights.W = arrayWeight[3];
                    }
                    if (arrayBoneId.Length > 2)
                    {
                        v[arrayIndex[2]].blendIndices.Z = arrayBoneId[2]; // " entry 3
                        v[arrayIndex[2]].blendWeights.Z = arrayWeight[2];
                    }
                    if (arrayBoneId.Length > 1)
                    {
                        v[arrayIndex[1]].blendIndices.Y = arrayBoneId[1]; // " entry 2
                        v[arrayIndex[1]].blendWeights.Y = arrayWeight[1];
                    }
                    if (arrayBoneId.Length > 0)
                    {
                        v[arrayIndex[0]].blendIndices.X = arrayBoneId[0]; // " entry 1
                        v[arrayIndex[0]].blendWeights.X = arrayWeight[0];
                    }
                }
            }
            model.meshes[mi].vertices = v;       // refer to vertices and indices we just setup
            model.meshes[mi].indices = indices;
            // reverse winding if specified (i2 and i1 are swapped to flip winding direction)
            if (ReverseVerticeWinding)
            {
                for (int k = 0; k < model.meshes[mi].indices.Length; k += 3)
                {
                    var i0 = model.meshes[mi].indices[k + 0]; var i1 = model.meshes[mi].indices[k + 1]; var i2 = model.meshes[mi].indices[k + 2];
                    model.meshes[mi].indices[k + 0] = i0; model.meshes[mi].indices[k + 1] = i2; model.meshes[mi].indices[k + 2] = i1;
                }
            }

            for (int i = 0; i < model.meshes[mi].vertices.Length; i++)
            {
                LogManager.Instance.WriteLineDebug($"{i} {model.meshes[mi].vertices[i].pos}");
            }
        } // end-loop scene meshes vertex data
    }


    //--------------------------
    // G E T   F I L E   N A M E
    //--------------------------
    /// <summary> Custom get file name </summary>
    public string GetFileName(string s, bool useBothSeperators)
    {
        var tpathsplit = s.Split(new char[] { '.' });
        string f = tpathsplit[0];
        if (tpathsplit.Length > 1)
        {
            f = tpathsplit[tpathsplit.Length - 2];
        }

        if (useBothSeperators)
        {
            tpathsplit = f.Split(new char[] { '/', '\\' });
        }
        else
        {
            tpathsplit = f.Split(new char[] { '/' });
        }

        s = tpathsplit[tpathsplit.Length - 1];
        return s;
    }


    //----------------------------------
    // G E T  B O N E   F O R   M E S H 
    //----------------------------------
    /// <summary> Gets the named bone in the model mesh </summary>
    private bool GetBoneForMesh(SkinModel.SkinMesh sMesh, string name, out SkinModel.ModelBone bone, out int boneIndexInMesh)
    {
        bool found = false;
        bone = null;
        boneIndexInMesh = 0;
        for (int j = 0; j < sMesh.meshBones.Length; j++)
        {  // loop thru the bones of the mesh
            if (sMesh.meshBones[j].name == name)
            {          // found a bone whose name matches                     
                boneIndexInMesh = j;                        // return the index into the bone-list of the mesh
                bone = sMesh.meshBones[j];                 // return the matching bone                    
                found = true;
            }
        }
        return found;
    }


    //------------------------------
    // G E T   R E F   T O   N O D E  (by name) 
    //------------------------------
    /// <summary> Searches the model for the name of the node. If found, it returns the model node - else returns null. </summary>
    private static SkinModel.ModelNode GetRefToNode(string name, SkinModel.ModelNode node)
    {
        SkinModel.ModelNode result = null;
        if (node.name == name)
        {
            return node;
        }

        if (result == null && node.children.Count > 0)
        {         // must have the result == null && because remember - this is recursive 
            for (int i = 0; i < node.children.Count; i++)
            {
                result = GetRefToNode(name, node.children[i]);
                if (result != null)
                {
                    return result;          // found it
                }
            }
        }
        return result;
    }


    // C L A S S   T E M P   V E R T   W E I G H T   I N D I C E S
    public class TempVertWeightIndices
    {
        public int numBonesForThisVert = 0;
        public List<float> vertFlatBoneId = new();
        public List<int> vertIndices = new();
        public List<float> vertBoneWeights = new();
    }
}