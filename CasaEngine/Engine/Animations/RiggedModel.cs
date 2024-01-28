using CasaEngine.Core.Log;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


/*  TODO 
//  lots to do still and think about.
// Fix the amimations there is a small discrepancy between my animations and visual studios model viewer which also isn't perfect it is irking me.
// link the textures to the shader in the model 
//  a) change the texture for diffuse to a list so a model can allow for multi texturing i dunno how many or if any models actually use this.
//     ... however assimp holds texture arrays for diffuse uv textures and others so it seems to be something that exists ill have to go back and change that.
//  b) add in the normal mapping to the shader. 
//  c) add in a normal map generator algorithm, this would be nice.
//  d) add in heightmapping humm i never actually got around to writing one yet so that will need a seperate test project with a proof of concept effect.
//
// Other kinds of animations need to be added but since these are primarily world space transforms that will be quite a bit easier.
// I just need to decide how to handle it, could even just use the bone 0 transform for that which i have added as a dummy node.
//
// Deformer animations also need to be added i need to do a little more research before i add that 
// Althought the principal is simple.
// I have no idea how assimp intends these deformations to be used as mesh vertice deforms ir bone deforms for example ect...
// 
// Improve the draw method so it draws itself better improve the effect file so i can test the above stuff.
// I suppose i should read things like the specular and diffuse values ect... 
// but this is minor most people will use there own effect pixel shaders anyways.
// Maybe stick the effect into the model.
//
// There is also the notion of were these steps belong some maybe belong in the loader some maybe the model.
// there is also the question of do i want a seperate temporary model in the loader and then a seperate xnb written and read model later on.
// there is also the question of is that even smart at this point and maybe should it just be bypassed altogether and the files written in csv xml or as dats.
//
 */

/// https://github.com/willmotil/MonoGameUtilityClasses

namespace CasaEngine.Engine.Animations;

/// <summary>
/// The rigged model  i stuffed the classes the rigged model uses in it as nested classes just to keep it all together.
/// I don't really see anything else using these classes anyways they are really just specific to the model.
/// Even the loader should go out of scope when its done loading a model and then even it is just going to be a conversion tool.
/// After i make a content reader and writer for the model class there will be no need for the loader except to change over new models.
/// However you don't really have to have it in xnb form at all you could use it as is but it does a lot of processing so... meh...
/// </summary>
public class RiggedModel
{
    public bool ConsoleDebug = true;

    public Effect Effect;
    public int NumberOfBonesInUse = 0;
    public int NumberOfNodesInUse = 0;
    public readonly int MaxGlobalBones = 128; // 78       
    public readonly Matrix[] GlobalShaderMatrixs; // these are the real final bone matrices they end up on the shader.
    public readonly List<RiggedModelNode> FlatListToBoneNodes = new();
    public readonly List<RiggedModelNode> FlatListToAllNodes = new();
    public RiggedModelMesh[] Meshes;
    public RiggedModelNode RootNodeOfTree; // The actual model root node the base node of the model from here we can locate any node in the chain.
    public RiggedModelNode FirstRealBoneInTree; // unused as of yet. The actual first bone in the scene the basis of the users skeletal model he created.
                                                //public RiggedModelNode globalPreTransformNode; // the accumulated orientations and scalars prior to the first bone acts as a scalar to the actual bone local transforms from assimp.

    // initial assimp animations
    public readonly List<RiggedAnimation> OriginalAnimations = new();
    int _currentAnimation = 0;
    public int CurrentFrame = 0;
    public bool AnimationRunning = false;
    bool _loopAnimation = true;
    public float CurrentAnimationFrameTime = 0;

    /// <summary>
    /// Uses static animation frames instead of interpolated frames.
    /// </summary>
    public readonly bool UseStaticGeneratedFrames = false;

    // mainly for testing to step thru each frame.
    public float OverrideAnimationFrameTime = -1;

    /// <summary>
    /// Instantiates the model object and the boneShaderFinalMatrix array setting them all to identity.
    /// </summary>
    public RiggedModel()
    {
        GlobalShaderMatrixs = new Matrix[MaxGlobalBones];
        for (int i = 0; i < MaxGlobalBones; i++)
        {
            GlobalShaderMatrixs[i] = Matrix.Identity;
        }
    }

    /// <summary>
    /// As stated
    /// </summary>
    public void SetEffect(Effect effect, Texture2D t, Matrix world, Matrix view, Matrix projection)
    {
        Effect = effect;
        //texture = t;
        Effect.Parameters["TextureA"].SetValue(t);
        Effect.Parameters["World"].SetValue(world);
        Effect.Parameters["View"].SetValue(view);
        Effect.Parameters["Projection"].SetValue(projection);
        Effect.Parameters["Bones"].SetValue(GlobalShaderMatrixs);
    }

    /// <summary>
    /// As stated
    /// </summary>
    public void SetEffectTexture(Texture2D t)
    {
        Effect.Parameters["TextureA"].SetValue(t);
    }

    /// <summary>
    /// Convienience method pass the node let it set itself.
    /// This also allows you to call this is in a iterated node tree and just bypass setting non bone nodes.
    /// </summary>
    public void SetGlobalShaderBoneNode(RiggedModelNode n)
    {
        if (n.IsThisARealBone)
        {
            GlobalShaderMatrixs[n.BoneShaderFinalTransformIndex] = n.CombinedTransformMg;
        }
    }

    /// <summary>
    /// Update
    /// </summary>
    public void Update(float elapsedTime)
    {
        if (AnimationRunning)
        {
            UpdateModelAnimations(elapsedTime);
        }

        IterateUpdate(RootNodeOfTree);
        UpdateMeshTransforms();
    }

    /// <summary>
    /// Gets the animation frame corresponding to the elapsed time for all the nodes and loads them into the model node transforms.
    /// </summary>
    private void UpdateModelAnimations(float elapsedTime)
    {
        if (OriginalAnimations.Count > 0 && _currentAnimation < OriginalAnimations.Count)
        {
            CurrentAnimationFrameTime += elapsedTime;
            float animationTotalDuration;
            if (_loopAnimation)
            {
                animationTotalDuration = (float)OriginalAnimations[_currentAnimation].DurationInSecondsLooping;
            }
            else
            {
                animationTotalDuration = (float)OriginalAnimations[_currentAnimation].DurationInSeconds;
            }

            // just for seeing a single frame lets us override the current frame.
            if (OverrideAnimationFrameTime >= 0f)
            {
                CurrentAnimationFrameTime = OverrideAnimationFrameTime;
                if (OverrideAnimationFrameTime > animationTotalDuration)
                {
                    OverrideAnimationFrameTime = 0f;
                }
            }

            // if we are using static frames.
            CurrentFrame = (int)(CurrentAnimationFrameTime / OriginalAnimations[_currentAnimation].SecondsPerFrame);
            int numbOfFrames = OriginalAnimations[_currentAnimation].TotalFrames;

            // usually we aren't using static frames and we might be looping.
            if (CurrentAnimationFrameTime > animationTotalDuration)
            {
                if (_loopAnimation)
                {
                    CurrentAnimationFrameTime -= animationTotalDuration;
                }
                else // animation completed
                {
                    CurrentFrame = 0;
                    CurrentAnimationFrameTime = 0;
                    AnimationRunning = false;
                }
            }

            // use the precalculated frame time lookups.
            if (UseStaticGeneratedFrames)
            {
                // set the local node transforms from the frame.
                if (CurrentFrame < numbOfFrames)
                {
                    int nodeCount = OriginalAnimations[_currentAnimation].AnimatedNodes.Count;
                    for (int nodeLooped = 0; nodeLooped < nodeCount; nodeLooped++)
                    {
                        var animNodeframe = OriginalAnimations[_currentAnimation].AnimatedNodes[nodeLooped];
                        var node = animNodeframe.NodeRef;
                        node.LocalTransformMg = animNodeframe.FrameOrientations[CurrentFrame];
                    }
                }
            }

            // use the calculated interpolated frames directly
            if (UseStaticGeneratedFrames == false)
            {
                int nodeCount = OriginalAnimations[_currentAnimation].AnimatedNodes.Count;
                for (int nodeLooped = 0; nodeLooped < nodeCount; nodeLooped++)
                {
                    var animNodeframe = OriginalAnimations[_currentAnimation].AnimatedNodes[nodeLooped];
                    var node = animNodeframe.NodeRef;
                    // use dynamic interpolated frames
                    node.LocalTransformMg = OriginalAnimations[_currentAnimation].Interpolate(CurrentAnimationFrameTime, animNodeframe, _loopAnimation);
                }

            }
        }
    }

    /// <summary>
    /// Updates the node transforms
    /// </summary>
    private void IterateUpdate(RiggedModelNode node)
    {
        if (node.Parent != null)
        {
            node.CombinedTransformMg = node.LocalTransformMg * node.Parent.CombinedTransformMg;
        }
        else
        {
            node.CombinedTransformMg = node.LocalTransformMg;
        }

        //// humm little test
        //if (node.name == "Armature")
        //    node.CombinedTransformMg = Matrix.Identity;

        // set to the final shader matrix.
        if (node.IsThisARealBone)
        {
            GlobalShaderMatrixs[node.BoneShaderFinalTransformIndex] = node.OffsetMatrixMg * node.CombinedTransformMg;
        }

        // call children
        foreach (RiggedModelNode n in node.Children)
        {
            IterateUpdate(n);
        }
    }

    // ok ... in draw we should now be able to call on this in relation to the world transform.
    private void UpdateMeshTransforms()
    {
        // try to handle when we just have mesh transforms
        for (int i = 0; i < Meshes.Length; i++)
        {
            // This feels errr is hacky.
            //meshes[i].nodeRefContainingAnimatedTransform.CombinedTransformMg = meshes[i].nodeRefContainingAnimatedTransform.LocalTransformMg * meshes[i].nodeRefContainingAnimatedTransform.InvOffsetMatrixMg;
            if (OriginalAnimations[CurrentPlayingAnimationIndex].AnimatedNodes.Count > 1)
            {
                Meshes[i].NodeRefContainingAnimatedTransform.CombinedTransformMg = Matrix.Identity;
            }

        }
    }


    /// <summary>
    /// Sets the global final bone matrices to the shader and draws it.
    /// </summary>
    public void Draw(GraphicsDevice gd, Matrix world)
    {
        Effect.Parameters["Bones"].SetValue(GlobalShaderMatrixs);

        for (var index = 0; index < Meshes.Length; index++)
        {
            var mesh = Meshes[index];

            if (mesh.Texture == null) // TODO : handle mesh with no texture => color + transparency
            {
                continue;
            }

            Effect.Parameters["TextureA"].SetValue(mesh.Texture);
            // We will add in the mesh transform to the world thru the mesh we could do it to every single bone but this way saves a bunch of matrix multiplys. 
            //effect.Parameters["World"].SetValue(world * m.MeshCombinedFinalTransformMg);
            Effect.Parameters["World"].SetValue(world * mesh.NodeRefContainingAnimatedTransform.CombinedTransformMg); // same thing
            Effect.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, mesh.Vertices, 0,
                mesh.Vertices.Length, mesh.Indices, 0, mesh.Indices.Length / 3,
                VertexPositionTextureNormalTangentWeights.VertexDeclaration);
        }
    }

    public int CurrentPlayingAnimationIndex
    {
        get => _currentAnimation;
        set
        {
            var n = value;
            if (n >= OriginalAnimations.Count)
            {
                n = 0;
            }

            _currentAnimation = n;
        }
    }

    /// <summary>
    /// This takes the original assimp animations and calculates a complete steady orientation matrix per frame for the fps of the animation duration.
    /// </summary>
    public void CreateStaticAnimationLookUpFrames(int fps, bool addLoopingTime)
    {
        for (var index = 0; index < OriginalAnimations.Count; index++)
        {
            OriginalAnimations[index].SetAnimationFpsCreateFrames(fps, this, addLoopingTime);
        }
    }

    public void BeginAnimation(int animationIndex)
    {
        CurrentAnimationFrameTime = 0;
        _currentAnimation = animationIndex;
        AnimationRunning = true;
    }

    public void StopAnimation()
    {
        AnimationRunning = false;
    }


    /// <summary>
    /// Models are composed of meshes each with there own textures and sets of vertices associated to them.
    /// </summary>
    public class RiggedModelMesh
    {
        public RiggedModelNode NodeRefContainingAnimatedTransform;
        public string TextureName;
        public string TextureNormalMapName;
        public string TextureHeightMapName;
        public string TextureReflectionMapName;
        public Texture2D Texture;
        public Texture2D TextureNormalMap;
        public Texture2D TextureHeightMap;
        public Texture2D TextureReflectionMap;
        public VertexPositionTextureNormalTangentWeights[] Vertices;
        public int[] Indices;
        public string NameOfMesh = "";
        public int NumberOfIndices => Indices.Length;

        public int NumberOfVertices => Vertices.Length;

        public int MaterialIndex { get; set; }
        public Matrix LinkedNodesOffsetMg { get; set; }
        public Matrix MeshInitialTransformFromNodeMg { get; set; }
        public Matrix MeshCombinedFinalTransformMg { get; set; }
        /// <summary>
        /// Defines the minimum vertices extent in each direction x y z in system coordinates.
        /// </summary>
        public Vector3 Min { get; set; }
        /// <summary>
        /// Defines the mximum vertices extent in each direction x y z in system coordinates.
        /// </summary>
        public Vector3 Max { get; set; }
        /// <summary>
        /// Defines the center mass point or average of all the vertices.
        /// </summary>
        public Vector3 Centroid { get; set; }
    }

    /// <summary>
    /// A node of the rigged model is really a transform joint some are bones some aren't. These form a heirarchial linked tree structure.
    /// </summary>
    public class RiggedModelNode
    {
        public string Name = "";
        public int BoneShaderFinalTransformIndex = -1;
        public RiggedModelNode Parent;
        public readonly List<RiggedModelNode> Children = new();

        // probably don't need most of these they are from the debug phase.
        public bool IsTheRootNode = false;
        public bool IsTheGlobalPreTransformNode = false; // marks the node prior to the first bone...   (which is a accumulated pre transform multiplier to other bones)?.
        public bool IsTheFirstBone = false; // marked as root bone.
        public bool IsThisARealBone = false; // a actual bone with a bone offset.
        public bool IsANodeAlongTheBoneRoute = false; // similar to is isThisNodeTransformNecessary but can include the nodes after bones.
        public bool IsThisNodeTransformNecessary = false; // has a requisite transformation in this node that a bone will need later.
        public bool IsThisAMeshNode = false; // is this actually a mesh node.
        public readonly bool IsThisTheFirstMeshNode = false;
        //public RiggedModelMesh meshRef; // no point in this as there can be many refs per node we link in the opposite direction.

        /// <summary>
        /// The inverse offset takes one from model space to bone space to say it will have a position were the bone is in the world.
        /// It is of the world space transform type from model space.
        /// </summary>
        public Matrix InvOffsetMatrixMg
        {
            get => Matrix.Invert(OffsetMatrixMg);
            set => OffsetMatrixMg = Matrix.Invert(value);
        }

        /// <summary>
        /// Typically a chain of local transforms from bone to bone allow one bone to build off the next. 
        /// This is the inverse bind pose position and orientation of a bone or the local inverted bind pose e.g. inverse bone position at a node.
        /// The multiplication of this value by a full transformation chain at that specific node reveals the difference of its current model space orientations to its bind pose orientations.
        /// This is a tranformation from world space towards model space.
        /// </summary>
        public Matrix OffsetMatrixMg { get; set; } = Matrix.Identity;
        /// <summary>
        /// The simplest one to understand this is a transformation of a specific bone in relation to the previous bone.
        /// This is a world transformation that has local properties.
        /// </summary>
        public Matrix LocalTransformMg { get; set; } = Matrix.Identity;
        /// <summary>
        /// The multiplication of transforms down the tree accumulate this value tracks those accumulations.
        /// While the local transforms affect the particular orientation of a specific bone.
        /// While blender or other apps my allow some scaling or other adjustments from special matrices can be combined with this.
        /// This is a world space transformation. Basically the final world space transform that can be uploaded to the shader after all nodes are processed.
        /// </summary>
        public Matrix CombinedTransformMg { get; set; } = Matrix.Identity;
    }

    /// <summary>
    /// Animations for the animation structure i have all the nodes in the rigged animation and the nodes have lists of frames of animations.
    /// </summary>
    public class RiggedAnimation
    {
        public string TargetNodeConsoleName = "_none_"; //"L_Hand";

        public string AnimationName = "";
        public double DurationInTicks = 0;
        public double DurationInSeconds = 0;
        public double DurationInSecondsLooping = 0;
        public double TicksPerSecond = 0;
        public double SecondsPerFrame = 0;
        public double TicksPerFramePerSecond = 0;
        public int TotalFrames = 0;

        private int _fps = 0;

        //public int MeshAnimationNodeCount;
        public bool HasMeshAnimations = false;
        public bool HasNodeAnimations = false;
        public List<RiggedAnimationNodes> AnimatedNodes;


        public void SetAnimationFpsCreateFrames(int animationFramesPerSecond, RiggedModel model, bool loopAnimation)
        {
            Logs.WriteTrace("________________________________________________________");
            Logs.WriteTrace("Animation name: " + AnimationName + "  DurationInSeconds: " + DurationInSeconds + "  DurationInSecondsLooping: " + DurationInSecondsLooping);
            _fps = animationFramesPerSecond;
            TotalFrames = (int)(DurationInSeconds * (double)(animationFramesPerSecond));
            TicksPerFramePerSecond = TicksPerSecond / (double)(animationFramesPerSecond);
            SecondsPerFrame = (1d / (animationFramesPerSecond));
            CalculateNewInterpolatedAnimationFrames(model, loopAnimation);
        }

        private void CalculateNewInterpolatedAnimationFrames(RiggedModel model, bool loopAnimation)
        {
            // Loop nodes.
            for (int i = 0; i < AnimatedNodes.Count; i++)
            {
                // Make sure we have enough frame orientations alloted for the number of frames.
                AnimatedNodes[i].FrameOrientations = new Matrix[TotalFrames];
                AnimatedNodes[i].FrameOrientationTimes = new double[TotalFrames];

                // print name of node as we loop
                Logs.WriteTrace("name " + AnimatedNodes[i].NodeName);

                // Loop destination frames.
                for (int j = 0; j < TotalFrames; j++)
                {
                    // Find and set the interpolated value from the s r t elements based on time.
                    var frameTime = j * SecondsPerFrame; // + .0001d;
                    AnimatedNodes[i].FrameOrientations[j] = Interpolate(frameTime, AnimatedNodes[i], loopAnimation);
                    AnimatedNodes[i].FrameOrientationTimes[j] = frameTime;
                }
            }
        }


        /// <summary>
        /// ToDo when we are looping back i think i need to artificially increase the duration in order to get a slightly smoother animation from back to front.
        /// </summary>
        public Matrix Interpolate(double animTime, RiggedAnimationNodes n, bool loopAnimation)
        {
            var durationSecs = DurationInSeconds;
            if (loopAnimation)
            {
                durationSecs = DurationInSecondsLooping;
            }

            while (animTime > durationSecs)
                animTime -= durationSecs;

            var nodeAnim = n;
            // 
            Quaternion q2 = nodeAnim.Qrot[0];
            Vector3 p2 = nodeAnim.Position[0];
            Vector3 s2 = nodeAnim.Scale[0];
            double tq2 = nodeAnim.QrotTime[0];
            double tp2 = nodeAnim.PositionTime[0]; ;
            double ts2 = nodeAnim.ScaleTime[0];
            // 
            int i1 = 0;
            Quaternion q1 = nodeAnim.Qrot[i1];
            Vector3 p1 = nodeAnim.Position[i1];
            Vector3 s1 = nodeAnim.Scale[i1];
            double tq1 = nodeAnim.QrotTime[i1];
            double tp1 = nodeAnim.PositionTime[i1];
            double ts1 = nodeAnim.ScaleTime[i1];
            // 
            int qindex2 = 0; int qindex1 = 0;
            int pindex2 = 0; int pindex1 = 0;
            int sindex2 = 0; int sindex1 = 0;
            //
            var qiat = nodeAnim.QrotTime[^1];
            if (animTime > qiat)
            {
                tq1 = nodeAnim.QrotTime[^1];
                q1 = nodeAnim.Qrot[^1];
                tq2 = nodeAnim.QrotTime[0] + durationSecs;
                q2 = nodeAnim.Qrot[0];
                qindex1 = nodeAnim.Qrot.Count - 1;
                qindex2 = 0;
            }
            else
            {
                //
                for (int frame2 = nodeAnim.Qrot.Count - 1; frame2 > -1; frame2--)
                {
                    var t = nodeAnim.QrotTime[frame2];
                    if (animTime <= t)
                    {
                        //1___
                        q2 = nodeAnim.Qrot[frame2];
                        tq2 = nodeAnim.QrotTime[frame2];
                        qindex2 = frame2; // for output to console only
                                          //2___
                        var frame1 = frame2 - 1;
                        if (frame1 < 0)
                        {
                            frame1 = nodeAnim.Qrot.Count - 1;
                            tq1 = nodeAnim.QrotTime[frame1] - durationSecs;
                        }
                        else
                        {
                            tq1 = nodeAnim.QrotTime[frame1];
                        }
                        q1 = nodeAnim.Qrot[frame1];
                        qindex1 = frame1; // for output to console only
                    }
                }
            }
            //
            var piat = nodeAnim.PositionTime[^1];
            if (animTime > piat)
            {
                tp1 = nodeAnim.PositionTime[^1];
                p1 = nodeAnim.Position[^1];
                tp2 = nodeAnim.PositionTime[0] + durationSecs;
                p2 = nodeAnim.Position[0];
                pindex1 = nodeAnim.Position.Count - 1;
                pindex2 = 0;
            }
            else
            {
                for (int frame2 = nodeAnim.Position.Count - 1; frame2 > -1; frame2--)
                {
                    var t = nodeAnim.PositionTime[frame2];
                    if (animTime <= t)
                    {
                        //1___
                        p2 = nodeAnim.Position[frame2];
                        tp2 = nodeAnim.PositionTime[frame2];
                        pindex2 = frame2; // for output to console only
                                          //2___
                        var frame1 = frame2 - 1;
                        if (frame1 < 0)
                        {
                            frame1 = nodeAnim.Position.Count - 1;
                            tp1 = nodeAnim.PositionTime[frame1] - durationSecs;
                        }
                        else
                        {
                            tp1 = nodeAnim.PositionTime[frame1];
                        }
                        p1 = nodeAnim.Position[frame1];
                        pindex1 = frame1; // for output to console only
                    }
                }
            }
            // scale
            var siat = nodeAnim.ScaleTime[^1];
            if (animTime > siat)
            {
                ts1 = nodeAnim.ScaleTime[^1];
                s1 = nodeAnim.Scale[^1];
                ts2 = nodeAnim.ScaleTime[0] + durationSecs;
                s2 = nodeAnim.Scale[0];
                sindex1 = nodeAnim.Scale.Count - 1;
                sindex2 = 0;
            }
            else
            {
                for (int frame2 = nodeAnim.Scale.Count - 1; frame2 > -1; frame2--)
                {
                    var t = nodeAnim.ScaleTime[frame2];
                    if (animTime <= t)
                    {
                        //1___
                        s2 = nodeAnim.Scale[frame2];
                        ts2 = nodeAnim.ScaleTime[frame2];
                        sindex2 = frame2; // for output to console only
                                          //2___
                        var frame1 = frame2 - 1;
                        if (frame1 < 0)
                        {
                            frame1 = nodeAnim.Scale.Count - 1;
                            ts1 = nodeAnim.ScaleTime[frame1] - durationSecs;
                        }
                        else
                        {
                            ts1 = nodeAnim.ScaleTime[frame1];
                        }
                        s1 = nodeAnim.Scale[frame1];
                        sindex1 = frame1; // for output to console only
                    }
                }
            }


            float tqi;
            float tpi;
            float tsi;

            Quaternion q;
            if (qindex1 != qindex2)
            {
                tqi = (float)GetInterpolationTimeRatio(tq1, tq2, animTime);
                q = Quaternion.Slerp(q1, q2, tqi);
            }
            else
            {
                tqi = (float)tq2;
                q = q2;
            }

            Vector3 p;
            if (pindex1 != pindex2)
            {
                tpi = (float)GetInterpolationTimeRatio(tp1, tp2, animTime);
                p = Vector3.Lerp(p1, p2, tpi);
            }
            else
            {
                tpi = (float)tp2;
                p = p2;
            }

            Vector3 s;
            if (sindex1 != sindex2)
            {
                tsi = (float)GetInterpolationTimeRatio(ts1, ts2, animTime);
                s = Vector3.Lerp(s1, s2, tsi);
            }
            else
            {
                tsi = (float)ts2;
                s = s2;
            }

            ////if (targetNodeConsoleName == n.nodeName || targetNodeConsoleName == "")
            ////{
            //    Logs.WriteTrace("" + "AnimationTime: " + animTime.ToStringTrimed());
            //    Logs.WriteTrace(" q : " + " index1: " + qindex1 + " index2: " + qindex2 + " time1: " + tq1.ToStringTrimed() + "  time2: " + tq2.ToStringTrimed() + "  interpolationTime: " + tqi.ToStringTrimed() + "  quaternion: " + q.ToStringTrimed());
            //    Logs.WriteTrace(" p : " + " index1: " + pindex1 + " index2: " + pindex2 + " time1: " + tp1.ToStringTrimed() + "  time2: " + tp2.ToStringTrimed() + "  interpolationTime: " + tpi.ToStringTrimed() + "  position: " + p.ToStringTrimed());
            //    Logs.WriteTrace(" s : " + " index1: " + sindex1 + " index2: " + sindex2 + " time1: " + ts1.ToStringTrimed() + "  time2: " + ts2.ToStringTrimed() + "  interpolationTime: " + tsi.ToStringTrimed() + "  scale: " + s.ToStringTrimed());
            ////}

            //s *= .01f;

            var ms = Matrix.CreateScale(s);
            var mr = Matrix.CreateFromQuaternion(q);
            var mt = Matrix.CreateTranslation(p);
            var m = mr * ms * mt;
            //var m = mr  * mt;
            return m;
        }

        public double GetInterpolationTimeRatio(double s, double e, double val)
        {
            if (val < s || val > e)
            {
                throw new Exception("RiggedModel.cs RiggedAnimation GetInterpolationTimeRatio the value " + val + " passed to the method must be within the start and end time. ");
            }

            return (val - s) / (e - s);
        }

    }

    /// <summary>
    /// Each node contains lists for Animation frame orientations. 
    /// The initial srt transforms are copied from assimp and a static interpolated orientation frame time set is built.
    /// This is done for the simple reason of efficiency and scalable computational look up speed. 
    /// The trade off is a larger memory footprint per model that however can be mitigated.
    /// </summary>
    public class RiggedAnimationNodes
    {
        public RiggedModelNode NodeRef;
        public string NodeName = "";
        // in model tick time
        public readonly List<double> PositionTime = new();
        public readonly List<double> ScaleTime = new();
        public readonly List<double> QrotTime = new();
        public readonly List<Vector3> Position = new();
        public readonly List<Vector3> Scale = new();
        public readonly List<Quaternion> Qrot = new();

        // the actual calculated interpolation orientation matrice based on time.
        public double[] FrameOrientationTimes;
        public Matrix[] FrameOrientations;
    }

}