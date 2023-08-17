//#define USING_COLORED_VERTICES  // uncomment this in both SkinModel and SkinModelLoader if using
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// ASSIMP INSTRUCTIONS:
// AssimpNET is (cross platform) .NET wrapper for Open Asset Import Library 
// Add the AssimpNET nuget to your solution:
// - in the solution explorer, right click on the project
// - select manage nuget packages
// - click browse
// - type in assimpNET and install it to the solution and project via the checkbox on the right.

/// THIS IS BASED ON WORK BY:  WIL MOTIL (a modified slightly older version)
/// https://github.com/willmotil/MonoGameUtilityClasses

namespace CasaEngine.Engine.Animations;

// C L A S S   S K I N M O D E L 
public class SkinModel
{
    // S E T T I N G S
    public const bool WARN_MISSING_DIFFUSE_TEX = false;

    GraphicsDevice _graphicsDevice;
    SkinFx skinFx;                // using to control SkinEffect             
    public Texture2D debug_tex;
    public bool use_debug_tex;
    public int max_bones = 180;
    public Matrix[] skinShaderMatrices;    // these are the real final bone matrices they end up on the shader
    public SkinMesh[] meshes;
    public ModelNode rootNodeOfTree;        // actual model root node - base node of the model - from here we can locate any node in the chain        

    // animations
    public List<RigAnimation> Animations { get; } = new();
    int currentAnim;
    public int currentFrame;
    public bool animationRunning;
    bool loopAnimation = true;
    public float currentAnimFrameTime;
    public float overrideAnimFrameTime = -1;  // mainly for testing to step thru each frame


    //----------------------
    // C O N S T R U C T O R
    //----------------------
    public SkinModel(GraphicsDevice graphicsDevice, SkinFx skin_effect)
    {
        _graphicsDevice = graphicsDevice;
        skinFx = skin_effect;
        skinShaderMatrices = new Matrix[max_bones];
        ResetShaderMatrices();
    }
    //------------------------------------------
    // R E S E T   S H A D E R   M A T R I C E S 
    //------------------------------------------
    public void ResetShaderMatrices()
    {
        for (int i = 0; i < max_bones; i++)
        {
            skinShaderMatrices[i] = Matrix.Identity;
        }
    }

    //--------------------
    // S E T   E F F E C T (not tested - WIP - may be useful in Game1 if rendering a particular sub-mesh with a different skin-effect)
    //--------------------
    public void SetEffect(Effect fx, Matrix viewProjMatrix, Matrix viewMatrix, Vector3 cameraPosition, Matrix world, Texture2D tex, bool set_bones = true, Texture2D normalMap = null, Texture2D specularMap = null)
    {
        skinFx.fx = fx;             // this assumes the alternative skin effect is already initialized in game1 
        skinFx.SetDrawParams(world, viewProjMatrix, viewMatrix, cameraPosition, tex, normalMap, specularMap);
        if (set_bones)
        {
            skinFx.fx.Parameters["Bones"].SetValue(skinShaderMatrices); // (this may have been set already)
        }
    }
    //------------------------------------
    // S E T   E F F E C T   T E X T U R E (not tested - might be useful to swap shirts or something for a sub-mesh) 
    //------------------------------------
    public void SetEffectTexture(Texture2D tex, Texture2D normMap = null, Texture2D specMap = null)
    {
        skinFx.fx.Parameters["TexDiffuse"].SetValue(tex);
        if (normMap != null)
        {
            skinFx.fx.Parameters["TexNormalMap"].SetValue(tex);
        }

        if (specMap != null)
        {
            skinFx.fx.Parameters["TexSpecular"].SetValue(tex);
        }
    }


    //------------
    // U P D A T E
    //------------
    public void Update(float elapsedTime)
    {
        if (animationRunning)
        {
            UpdateModelAnimations(elapsedTime); // determine local transforms for animation
        }

        UpdateNodes(rootNodeOfTree);                           // update the skeleton 
        UpdateMeshAnims();                                     // update any regular mesh animations
    }


    //----------------------------------------------
    // U P D A T E   M O D E L   A N I M A T I O N S
    //----------------------------------------------
    ///<summary> Gets the animation frame (based on elapsed time) for all nodes and loads them into the model node transforms. </summary>
    private void UpdateModelAnimations(float elapsedTime)
    {
        if (Animations.Count <= 0 || currentAnim >= Animations.Count)
        {
            return;
        }

        AnimationTimeLogic(elapsedTime);                                      // process what to do based on animation time (frames, duration, complete | looping)

        int cnt = Animations[currentAnim].animatedNodes.Count;             // loop thru animated nodes
        for (int n = 0; n < cnt; n++)
        {
            AnimNodes animNode = Animations[currentAnim].animatedNodes[n]; // get animation keyframe lists (each animNode)
            ModelNode node = animNode.nodeRef;                         // get bone associated with this animNode (could be mesh-node) 
            node.local_mtx = Animations[currentAnim].Interpolate(currentAnimFrameTime, animNode); // interpolate keyframes (animate local matrix) for this bone
        }
    }

    //----------------------------------------
    // A N I M A T I O N   T I M E   L O G I C 
    //----------------------------------------
    /// <summary> What to do at a certain animation time. </summary>
    public void AnimationTimeLogic(float elapsedTime)
    {
        currentAnimFrameTime += elapsedTime;
        float animTotalDuration = (float)Animations[currentAnim].DurationInSeconds + (float)Animations[currentAnim].DurationInSecondsAdded; // add extra for looping

        // if we need to see a single frame; let us override the current frame
        if (overrideAnimFrameTime >= 0f)
        {
            currentAnimFrameTime = overrideAnimFrameTime;
            if (overrideAnimFrameTime > animTotalDuration)
            {
                overrideAnimFrameTime = 0f;
            }
        }

        if (currentAnimFrameTime > animTotalDuration)
        {
            if (loopAnimation)
            {
                currentAnimFrameTime -= animTotalDuration; // loop back to start
            }
            else
            {
                currentFrame = 0;  // assuming we might want to restart the animation later (from 0) 
                currentAnimFrameTime = 0;
                animationRunning = false;
            }
        }
    }


    //------------------------
    // U P D A T E   N O D E S 
    //------------------------
    /// <summary> Updates the skeleton (combined) after updating the local animated transforms </summary>
    private void UpdateNodes(ModelNode node)
    {
        // if there's a parent, we can add the local bone onto it to get the resulting bone location in skeleton:
        if (node.parent != null)
        {
            node.combined_mtx = node.local_mtx * node.parent.combined_mtx;
        }
        else
        {
            node.combined_mtx = node.local_mtx;  // no parent so just provide the local matrix transform
        }

        // loop thru the flat-list of bones for this node (bone could effect more than 1 mesh):
        for (int i = 0; i < node.uniqueMeshBones.Count; i++)
        {
            ModelBone bn = node.uniqueMeshBones[i];                // refers to the bone in uniqueMeshBones list (holds mesh#, bone#, etc)
            // Update the shader matrix (final bone transform) - we combine with the offset matrix to negate the original inverse we used 
            // to be able to do local vertex transforms correctly (which we did because vertices start out relative to a bind pose which we need to find a 
            // version where that's not so (for local transforms to work correctly) [ thus we used the inverse bind on the original bind-pose bones ]) 
            // So by adding the offset_mtx back on, the vertices will be transformed in a bind-relative way and reach the correct destination
            meshes[bn.meshIndex].shader_matrices[bn.boneIndex] = bn.offset_mtx * node.combined_mtx; // converts resulting vert transforms back to bind-pose-relative space
        }
        foreach (ModelNode n in node.children)
        {
            UpdateNodes(n);     // do same for children
        }
    }


    //----------------------------------
    // U P D A T E   M E S H   A N I M S
    //----------------------------------
    /// In draw, this should enable us to call on this in relation to the world transform.
    private void UpdateMeshAnims()
    {
        if (Animations.Count <= 0)
        {
            return;
        }

        for (int i = 0; i < meshes.Length; i++)
        {
            if (Animations[currentAnim].animatedNodes.Count > 0) // try to handle when we just have mesh transforms 
            {
                meshes[i].node_with_anim_trans.combined_mtx = Matrix.Identity;// clear out the combined transforms
            }
        }
    }

    public int CurrentAnimationIndex
    {
        get { return currentAnim; }
        set
        {
            var n = value;
            if (n >= Animations.Count)
            {
                n = 0;
            }

            currentAnim = n;
        }
    }

    public void BeginAnimation(int animationIndex)
    {
        currentAnimFrameTime = 0;
        currentAnim = animationIndex;                              // set current animation
        animationRunning = true;
    }

    public void StopAnimation()
    {
        animationRunning = false;
    }


    //---------
    // D R A W 
    //---------
    ///<summary> Sets final bone matrices to shader and draws - this Draw method, assumes entire character follows 1 world matrix and uses 1 shader/style
    /// Use a mesh loop (like example) to make distinct treatments of different character parts(meshes) and use DrawMesh overload that works for you. </summary>
    /// Note: use_mesh_materials = false - means default material setting on everything, otherwise it'll try to use loaded material values
    ///       use_material_spec  = true  - would allow each material to receive its own material-specified lighting color
    public void Draw(Matrix viewProjMatrix, Matrix viewMatrix, Vector3 cameraPosition, Matrix world, bool use_mesh_materials = true, bool use_material_spec = false)
    {
        foreach (SkinMesh skinMesh in meshes)
        {
            skinFx.fx.Parameters["Bones"].SetValue(skinMesh.shader_matrices);   // provide the bone matrices of this mesh
            if (use_mesh_materials)
            {
                AssignMaterials(skinMesh, use_material_spec);
            }

            // DO LAST (this will apply the technique with any parameters set before it (or in it)): 
            skinFx.SetDrawParams(world * skinMesh.node_with_anim_trans.combined_mtx,
                viewProjMatrix, viewMatrix, cameraPosition,
                skinMesh.tex_diffuse, skinMesh.tex_normalMap, skinMesh.tex_specular);
            _graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                skinMesh.vertices, 0, skinMesh.vertices.Length,
                skinMesh.indices, 0, skinMesh.indices.Length / 3,
                VertexNormMapSkin.VertexDeclaration);
        }
    }


    //--------------------------------
    // A S S I G N   M A T E R I A L S
    //--------------------------------
    private void AssignMaterials(SkinMesh m, bool use_material_spec)
    {
        skinFx.ambientCol.X = m.ambient.X; skinFx.ambientCol.Y = m.ambient.Y; skinFx.ambientCol.Z = m.ambient.Z;  //Vec4 to Vec3
        skinFx.emissiveCol.X = m.emissive.X; skinFx.emissiveCol.Y = m.emissive.Y; skinFx.emissiveCol.Z = m.emissive.Z; //Vec4 to Vec3
        skinFx.diffuseCol = m.diffuse;
        if (use_material_spec)
        {
            skinFx.specularCol.X = m.specular.X; skinFx.specularCol.Y = m.specular.Y; skinFx.specularCol.Z = m.specular.Z;
            skinFx.specularPow = m.shineStrength; // I think...                   
        }
    }


    // D R A W   M E S H  (by name)
    /// <summary> For a special case were a person may wish to manipulate each mesh by name. Could be optimized by dictionary/hash. </summary>
    public void DrawMeshByName(string meshNodeName, Matrix viewProjMatrix, Matrix viewMatrix, Vector3 cameraPosition, Matrix world, bool use_mesh_materials = true, bool use_material_spec = false)
    {
        int index = -1;
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i].node_with_anim_trans.name == meshNodeName)
            {
                index = i;
                i = meshes.Length;
            }
        }
        if (index > -1)
        {
            DrawMesh(index, viewProjMatrix, viewMatrix, cameraPosition, world, use_mesh_materials, use_material_spec);
        }
    }


    /// We might use this one a lot - great for looping thru meshes in game and specifying unique parameters for each mesh based on what it is
    /// Note: It would be possible to send in a transform in the above to add a temporary offset to a bone (like to aim the head in another direction) 
    //---------------------------------------------------
    // D R A W   M E S H  (by index)
    //---------------------------------------------------
    /// <summary> Draws the mesh by the index. </summary>
    public void DrawMesh(int meshIndex, Matrix viewProjMatrix, Matrix viewMatrix, Vector3 cameraPosition, Matrix world, bool use_mesh_materials = true, bool use_material_spec = false)
    {
        var m = meshes[meshIndex];
        skinFx.fx.Parameters["Bones"].SetValue(m.shader_matrices);     // provide the bone matrices of this mesh
        if (use_mesh_materials)
        {
            AssignMaterials(m, use_material_spec);
        }

        // DO LAST (this will apply the technique with any parameters set before it (or in it)): 
        skinFx.SetDrawParams(world * m.node_with_anim_trans.combined_mtx, viewProjMatrix, viewMatrix, cameraPosition,
            m.tex_diffuse, m.tex_normalMap, m.tex_specular);
        _graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, m.vertices, 0, m.vertices.Length, m.indices, 0, m.indices.Length / 3, VertexNormMapSkin.VertexDeclaration);
    }


    // M E S H   D E B U G   D R A W 
    /// <summary> Sets the global final bone matrices to the shader and draws it. </summary>
    public void MeshDebugDraw(GraphicsDevice gd, Matrix world, int meshIdToShow)
    {
        skinFx.fx.Parameters["Bones"].SetValue(skinShaderMatrices);
        for (int i = 0; i < meshes.Length; i++)
        {
            var m = meshes[i];
            AssignMaterials(m, true);
            if (i == meshIdToShow)
            {
                skinFx.fx.Parameters["World"].SetValue(world * m.CombinedFinalTransform);
                skinFx.fx.CurrentTechnique.Passes[0].Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, m.vertices, 0, m.vertices.Length, m.indices, 0, m.indices.Length / 3, VertexNormMapSkin.VertexDeclaration);
            }
        }
    }


    // C L A S S   M O D E L   B O N E 
    public class ModelBone
    {
        public string name;                  // bone name
        public int meshIndex = -1;        // which mesh? 
        public int boneIndex = -1;        // which bone? 
        public int numWeightedVerts = 0;  // number of weighted verts that use this bone for influence
        public Matrix InvOffset_mtx { get { return Matrix.Invert(offset_mtx); } } // inverse bind transforms (need vectors at model origin(0,0,0) when doing animation [add to parent's final transform after])
        public Matrix offset_mtx;            // bind-pose transforms
    } // Model Bone Class



    // C L A S S   M O D E L   N O D E 
    // a transform - some are bones(joints) - some not (part of tree). Each could link to more than 1 mesh and so have more than 1 offset.    
    public class ModelNode
    {
        public string name;                                // mesh or bone name (whichever it is)
        public ModelNode parent;                              // parent node       (usually parent bone) 
        public List<ModelNode> children = new();    // child tree

        public bool hasRealBone, isBoneOnRoute, isMeshNode; // used for debugging:       

        // Each mesh has a list of shader-matrices - this keeps track of which meshes these bones apply to (and the bone index)
        public List<ModelBone> uniqueMeshBones = new();  // points to mesh & bone that corresponds to this node bone.

        public Matrix local_mtx;     // transform relative to parent         
        public Matrix combined_mtx;  // tree-accumulated transforms  (global-space transform for shader to use - skin matrix)
    } // Model Node Class



    // C L A S S   S K I N   M E S H 
    /// <summary> Models are composed of meshes each with their own textures and sets of vertices associated to them. </summary>
    public class SkinMesh
    {
        public ModelNode node_with_anim_trans;  // reference of node containing animated transform
        public string Name = "";
        public int meshNumber;
        public bool hasBones;
        public bool hasMeshAnimAttachments = false;
        public string tex_name;
        public string tex_normMap_name;
        public string tex_heightMap_name;
        public string tex_reflectionMap_name;
        public string tex_specular_name;
        public Texture2D tex_diffuse;
        public Texture2D tex_specular;
        public Texture2D tex_normalMap;
        public Texture2D tex_heightMap;
        public Texture2D tex_reflectionMap;
        //public Texture2D tex_lightMap, tex_ambientOcclusion;     // maybe these 2 are better baked into tex_diffuse?            
        public VertexNormMapSkin[] vertices;
        public int[] indices;
        public ModelBone[] meshBones;
        public Matrix[] shader_matrices;

        public int material_index;
        public string material_name;               // (for this index)
        public Matrix CombinedFinalTransform { get { return node_with_anim_trans.combined_mtx; } }  // Final world position of mesh itself
        public Vector3 min, max, mid;

        // MESH MATERIAL (add more as needed - like if you want pbr):
        public Vector4 ambient = Vector4.One;   // minimum light color
        public Vector4 diffuse = Vector4.One;   // regular material colorization
        public Vector4 specular = Vector4.One;   // specular highlight color 
        public Vector4 emissive = Vector4.One;   // amplify a color brightness (not requiring light - similar to ambient really - kind of a glow without light)                
        public float opacity = 1.0f;     // how opaque or see-through is it?          
        public float reflectivity = 0.0f;     // strength of reflections
        public float shininess = 0.0f;     // how much light shines off
        public float shineStrength = 1.0f;     // probably specular power (can use to narrow & intensifies highlights - ie: more wet or metallic looking)
        public float bumpScale = 0.0f;     // amplify or reduce normal-map effect  
        public bool isTwoSided = false;    // useful for glass and ice
        //public Vector4 colorTransparent = Vector4.One;  
        //public Vector4 reflective = Vector4.One;
        //public float transparency = 0.0f;
        //public bool isPbrMaterial = false;
        //public string blendMode   = "Default";
        //public string shadingMode = "Default";
        //public bool hasShaders    = false;
        //public bool isWireFrameEnabled = false;
    } // Skin Mesh Class



    // C L A S S   R I G   A N I M A T I O N 
    /// <summary> All the 'animNodes' are in RigAnimation & the nodes have lists of frames of animations.</summary>
    public class RigAnimation
    {
        public string animation_name = "";
        public double DurationInTicks;        // how many ticks for whole animation
        public double DurationInSeconds;      // same in seconds
        public double DurationInSecondsAdded; // added seconds
        public double TicksPerSecond;         // ticks/sec (play speed)
        //public int    TotalFrames;            // total keyed frames

        public bool HasMeshAnims;           // contains mesh transform animations (usually no) 
        public bool HasNodeAnims;           // any node-based animations? 
        public List<AnimNodes> animatedNodes; // holds the animated nodes

        // I N T E R P O L A T E
        ///<summary> animation blending between key-frames </summary>
        public Matrix Interpolate(double animTime, AnimNodes nodeAnim)
        {
            var durationSecs = DurationInSeconds + DurationInSecondsAdded;

            while (animTime > durationSecs)        // If the requested play-time is past the end of the animation, loop it (ie: time = 20 but duration = 16 so time becomes 4)
                animTime -= durationSecs;

            Quaternion q1 = nodeAnim.qrot[0], q2 = q1;   // init rot as entry 0 for both keys (init may be needed cuz conditional value assignment can upset compiler)
            Vector3 p1 = nodeAnim.position[0], p2 = p1;   // " pos
            Vector3 s1 = nodeAnim.scale[0], s2 = s1;   // " scale
            double tq1 = nodeAnim.qrotTime[0], tq2 = tq1; // " rot-time
            double tp1 = nodeAnim.positionTime[0], tp2 = tp1; // " pos-time
            double ts1 = nodeAnim.scaleTime[0], ts2 = ts1; // " scale-time

            // GET ROTATION KEYFRAMES
            int end_t_index = nodeAnim.qrotTime.Count - 1;      // final time's index (starting with qrot cuz we do it first - we'll cahnge this variable for pos and scale)
            int end_index = nodeAnim.qrot.Count - 1;          // final rot frame
            var end_time = nodeAnim.qrotTime[end_t_index];   // get final rotation-time
            if (animTime > end_time)
            {                          // if animTime is past final rotation-time: Set to interpolate between last and first frames (for animation-loop)
                tq1 = end_time;                             // key 1 time is last keyframe and time 2 is time taken after to get to frame 0 (see below) 
                tq2 += durationSecs;                         // total duration accounting for time to loop from last frame to frame 0 (with DurationInSecondsAdded)
                q1 = nodeAnim.qrot[end_index];             // get final quaternion (count-1),       NOTE: q2 already set above (key frame 0)                                                                      
            }
            else
            {
                int frame2 = end_index, frame1;              //                  animTime   t =  frame2
                for (; frame2 > -1; frame2--)
                {              // loop from last index to 0 (until find correct place on timeline):
                    var t = nodeAnim.qrotTime[frame2];       // what's the time at this frame?
                    if (t < animTime)
                    {
                        break;                 // if the current_time > the frame time then we've found the spot we're looking for (break out)                                                    
                    }
                }
                if (frame2 < end_index)
                {
                    frame2++;            // at this point the frame2 is 1 less than what we're looking for so add 1
                }

                q2 = nodeAnim.qrot[frame2];
                tq2 = nodeAnim.qrotTime[frame2];
                frame1 = frame2 - 1;
                if (frame1 < 0)
                {
                    frame1 = end_index;                             // loop frame1 to last frame
                    tq1 = nodeAnim.qrotTime[frame1] - durationSecs; // Using: frame2time - frame1time, so we need time1 to be less _ thus: subtract durationSecs to fix it
                }
                else
                {
                    tq1 = nodeAnim.qrotTime[frame1];               // get time1 
                }

                q1 = nodeAnim.qrot[frame1];
            }
            // GET POSITION KEY FRAMES
            end_t_index = nodeAnim.positionTime.Count - 1;      // final time's index
            end_index = nodeAnim.position.Count - 1;          // final pos frame
            end_time = nodeAnim.positionTime[end_t_index];   // get final position-time
            if (animTime > end_time)
            {                          // if animTime is past final pos-time: Set to interpolate between last and first frames (for animation-loop)
                tp1 = end_time;                                // key 1 time is last keyframe and time 2 is time taken after to get to frame 0 (see below) 
                tp2 += durationSecs;                            // total duration accounting for time to loop from last frame to frame 0 (with DurationInSecondsAdded)
                p1 = nodeAnim.position[end_index];            // get final position (count-1),       NOTE: q2 already set above (key frame 0)                                                                      
            }
            else
            {
                int frame2 = end_index, frame1;
                for (; frame2 > -1; frame2--)
                {                 // loop from last index to 0 (until find correct place on timeline):
                    var t = nodeAnim.positionTime[frame2];      // what's the time at this frame?
                    if (t < animTime)
                    {
                        break;                    // if the current_time > the frame time then we've found the spot we're looking for (break out)                                                    
                    }
                }
                if (frame2 < end_index)
                {
                    frame2++;               // at this point the frame2 is 1 less than what we're looking for so add 1
                }

                p2 = nodeAnim.position[frame2];
                tp2 = nodeAnim.positionTime[frame2];
                frame1 = frame2 - 1;
                if (frame1 < 0)
                {
                    frame1 = end_index;                                 // loop frame1 to last frame
                    tp1 = nodeAnim.positionTime[frame1] - durationSecs; // Using: frame2time - frame1time, so we need time1 to be less _ thus: subtract durationSecs to fix it
                }
                else
                {
                    tp1 = nodeAnim.positionTime[frame1];               // get time1 
                }

                p1 = nodeAnim.position[frame1];
            }
            // GET SCALE KEYFRAMES 
            end_t_index = nodeAnim.scaleTime.Count - 1;         // final time's index
            end_index = nodeAnim.scale.Count - 1;             // final scale frame
            end_time = nodeAnim.scaleTime[end_t_index];      // get final scale-time
            if (animTime > end_time)
            {                          // if animTime is past final scale-time: Set to interpolate between last and first frames (for animation-loop)
                ts1 = end_time;                                // key 1 time is last keyframe and time 2 is time taken after to get to frame 0 (see below) 
                ts2 += durationSecs;                            // total duration accounting for time to loop from last frame to frame 0 (with DurationInSecondsAdded)
                s1 = nodeAnim.scale[end_index];               // get final scale (count-1),       NOTE: q2 already set above (key frame 0)                                                                      
            }
            else
            {
                int frame2 = end_index, frame1;
                for (; frame2 > -1; frame2--)
                {                 // loop from last index to 0 (until find correct place on timeline):
                    var t = nodeAnim.scaleTime[frame2];         // what's the time at this frame?
                    if (animTime > t)
                    {
                        break;                    // if the current_time > the frame time then we've found the spot we're looking for (break out)                                                    
                    }
                }
                if (frame2 < end_index)
                {
                    frame2++;               // at this point the frame2 is 1 less than what we're looking for so add 1
                }

                s2 = nodeAnim.scale[frame2];
                ts2 = nodeAnim.scaleTime[frame2];
                frame1 = frame2 - 1;
                if (frame1 < 0)
                {
                    frame1 = end_index;                               // loop frame1 to last frame
                    ts1 = nodeAnim.scaleTime[frame1] - durationSecs;  // Using: frame2time - frame1time, so we need time1 to be less _ thus: subtract durationSecs to fix it
                }
                else
                {
                    ts1 = nodeAnim.scaleTime[frame1];                // get time1 
                }

                s1 = nodeAnim.scale[frame1];
            }

            float tqi = 0, tpi = 0, tsi = 0;

            Quaternion q;
            tqi = (float)GetInterpolateTimePercent(tq1, tq2, animTime); // get the time% (0-1)
            q = Quaternion.Slerp(q1, q2, tqi);                          // blend the rotation between keys using the time percent

            Vector3 p;
            tpi = (float)GetInterpolateTimePercent(tp1, tp2, animTime); // "
            p = Vector3.Lerp(p1, p2, tpi);

            Vector3 s;
            tsi = (float)GetInterpolateTimePercent(ts1, ts2, animTime); // "
            s = Vector3.Lerp(s1, s2, tsi);

            var ms = Matrix.CreateScale(s);
            var mr = Matrix.CreateFromQuaternion(q);
            var mt = Matrix.CreateTranslation(p);

            var m = ms * mr * mt; // S,R,T
            return m;
        } // Interpolate


        // GET INTERPOLATE TIME PERCENT
        public double GetInterpolateTimePercent(double s, double e, double val)
        {
            if (val < s || val > e)
            {
                throw new Exception("SkinModel.cs RigAnimation GetInterpolateTimePercent :  Value " + val + " passed to the method must be within the start and end time. ");
            }

            if (s == e)
            {
                throw new Exception("SkinModel.cs RigAnimation GetInterpolateTimePercent :  e - s :  " + e + "-" + s + "=0  - Divide by zero error.");
            }

            return (val - s) / (e - s);
        }

    } // rig animation class




    // C L A S S   A N I M   N O D E S 
    /// <summary> Nodes contain animation frames. Initial trans are copied from assimp - then interpolated frame sets are built. (keeping original S,R,T if want to later edit) </summary>
    public class AnimNodes
    {
        public ModelNode nodeRef;
        public string nodeName = "";

        // in model tick time
        public List<double> positionTime = new();
        public List<double> scaleTime = new();
        public List<double> qrotTime = new();
        public List<Vector3> position = new();
        public List<Vector3> scale = new();
        public List<Quaternion> qrot = new();
    } // Anim Nodes Class
} // Skin Model Class



//--------------------------------------
public struct VertexNormMapSkin : IVertexType
{
    public Vector3 pos;
#if USING_COLORED_VERTICES
        public Vector4 color;
#endif
    public Vector2 uv;
    public Vector3 norm;
    public Vector3 tangent;
    public Vector3 biTangent;
    public Vector4 blendIndices;
    public Vector4 blendWeights;

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(BYT.Ini(3), VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
#if USING_COLORED_VERTICES
        new VertexElement(BYT.Ini(4), VertexElementFormat.Vector4, VertexElementUsage.Color, 0),
#endif
        new VertexElement(BYT.Off(2), VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(BYT.Off(3), VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(BYT.Off(3), VertexElementFormat.Vector3, VertexElementUsage.Tangent, 0),
        new VertexElement(BYT.Off(3), VertexElementFormat.Vector3, VertexElementUsage.Binormal, 0),
        new VertexElement(BYT.Off(4), VertexElementFormat.Vector4, VertexElementUsage.BlendIndices, 0),
        new VertexElement(BYT.Off(4), VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0)
    );

    VertexDeclaration IVertexType.VertexDeclaration { get { return VertexDeclaration; } }
}
// B O F F (adjusts byte offset for each entry in a vertex declaration)
public struct BYT
{
    public static int byt = 0;
    public static int Ini(int b_size) { b_size *= 4; byt = 0; byt += b_size; return 0; }
    public static int Off(int b_size) { b_size *= 4; byt += b_size; return byt - b_size; }
}