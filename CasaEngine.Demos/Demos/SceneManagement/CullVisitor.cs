using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Veldrid.SceneGraph.RenderGraph
{
    public class CullVisitor : NodeVisitor, ICullVisitor
    {
        public IRenderGroup OpaqueRenderGroup { get; set; } = RenderGroup.Create();
        public IRenderGroup TransparentRenderGroup { get; set; } = RenderGroup.Create();

        public GraphicsDevice GraphicsDevice { get; set; } = null;

        private Stack<Matrix> ModelMatrixStack { get; set; } = new Stack<Matrix>();

        private readonly Stack<IPipelineState> PipelineStateStack = new Stack<IPipelineState>();

        public bool Valid => null != GraphicsDevice;

        private Polytope CullingFrustum { get; } = new();

        public int RenderElementCount { get; private set; } = 0;

        private Matrix ViewMatrix { get; set; } = Matrix.Identity;
        private Matrix ProjectionMatrix { get; set; } = Matrix.Identity;

        public CullVisitor() :
            base(VisitorType.CullAndAssembleVisitor, TraversalModeType.TraverseActiveChildren)
        {
            ModelMatrixStack.Push(Matrix.Identity);
        }

        public void Reset()
        {
            ModelMatrixStack.Clear();
            ModelMatrixStack.Push(Matrix.Identity);

            PipelineStateStack.Clear();
            OpaqueRenderGroup.Reset();
            TransparentRenderGroup.Reset();

            RenderElementCount = 0;

        }

        public void SetViewMatrix(Matrix viewMatrix)
        {
            ViewMatrix = viewMatrix;
        }

        public void SetProjectionMatrix(Matrix projectionMatrix)
        {
            ProjectionMatrix = projectionMatrix;
        }

        public void Prepare()
        {
            var vp = ViewMatrix * ProjectionMatrix;
            CullingFrustum.SetViewProjectionMatrix(vp);
        }

        private Matrix GetModelViewMatrix()
        {
            return ModelMatrixStack.Peek() * ViewMatrix;
        }

        private Matrix GetModelViewInverseMatrix()
        {
            var viewMatrix = ModelMatrixStack.Peek() * ViewMatrix;
            Matrix.Invert(ref viewMatrix, out var inverse);
            return inverse;
        }

        private Vector3 GetEyeLocal()
        {
            var eyeWorld = Vector3.Zero;
            var modelViewInverse = GetModelViewInverseMatrix();
            return Vector3.Transform(eyeWorld, modelViewInverse);
        }

        private bool IsCulled(BoundingBox bb, Matrix modelMatrix)
        {
            var culled = !CullingFrustum.Contains(bb, modelMatrix);

            return culled;
        }

        public override void Apply(INode node)
        {
            var needsPop = false;
            if (node.HasPipelineState)
            {
                PipelineStateStack.Push(node.PipelineState);
                needsPop = true;
            }

            Traverse(node);

            if (needsPop)
            {
                PipelineStateStack.Pop();
            }
        }

        public override void Apply(ITransform transform)
        {
            var curModel = ModelMatrixStack.Peek();
            transform.ComputeLocalToWorldMatrix(ref curModel, this);
            ModelMatrixStack.Push(curModel);

            Apply((Node)transform);

            ModelMatrixStack.Pop();
        }


        public override void Apply(IGeode geode)
        {
            var bb = geode.GetBoundingBox();
            if (IsCulled(bb, ModelMatrixStack.Peek())) return;

            IPipelineState pso = null;

            // Node specific state
            if (geode.HasPipelineState)
            {
                pso = geode.PipelineState;
            }
            else if (PipelineStateStack.Count != 0) // Shared State
            {
                pso = PipelineStateStack.Peek();
            }
            else // Fallback
            {
                pso = new PipelineState();
            }

            foreach (var drawable in geode.Drawables)
            {
                if (IsCulled(drawable.GetBoundingBox(), ModelMatrixStack.Peek())) continue;

                var drawablePso = pso;
                if (drawable.HasPipelineState)
                {
                    drawablePso = drawable.PipelineState;
                }

                //
                // This allocates / updates vbo/ibos
                //
                drawable.ConfigureDeviceBuffers(GraphicsDevice);

                var renderElementCache = new Dictionary<IRenderGroupState, RenderGroupElement>();

                foreach (var pset in drawable.PrimitiveSets)
                {
                    if (IsCulled(pset.GetBoundingBox(), ModelMatrixStack.Peek())) continue;

                    //            
                    // Sort into appropriate render group
                    // 
                    IRenderGroupState renderGroupState = null;
                    if (drawablePso.BlendStateDescription.AttachmentStates.Contains(BlendAttachmentDescription.AlphaBlend))
                    {
                        renderGroupState = TransparentRenderGroup.GetOrCreateState(GraphicsDevice, drawablePso, pset.PrimitiveType, drawable.VertexLayout);
                    }
                    else
                    {
                        renderGroupState = OpaqueRenderGroup.GetOrCreateState(GraphicsDevice, drawablePso, pset.PrimitiveType, drawable.VertexLayout);
                    }

                    if (false == renderElementCache.TryGetValue(renderGroupState, out var renderElement))
                    {
                        renderElement = new RenderGroupElement()
                        {
                            ModelViewMatrix = GetModelViewMatrix(),
                            VertexBuffer = drawable.GetVertexBufferForDevice(GraphicsDevice),
                            IndexBuffer = drawable.GetIndexBufferForDevice(GraphicsDevice),
                            PrimitiveSets = new List<IPrimitiveSet>()
                        };
                        renderGroupState.Elements.Add(renderElement);

                        renderElementCache.Add(renderGroupState, renderElement);
                    }
                    renderElement.PrimitiveSets.Add(pset);
                }
            }
        }

        /*
        public override void Apply(IBillboard billboard)
        {
            var bb = billboard.GetBoundingBox();
            if (IsCulled(bb, ModelMatrixStack.Peek())) return;

            IPipelineState pso = null;

            // Node specific state
            if (billboard.HasPipelineState)
            {
                pso = billboard.PipelineState;
            }

            // Shared State
            else if (PipelineStateStack.Count != 0)
            {
                pso = PipelineStateStack.Peek();
            }

            // Fallback
            else
            {
                pso = PipelineState.Create();
            }

            var eyeLocal = GetEyeLocal();
            var modelView = GetModelViewMatrix();

            foreach (var drawable in billboard.Drawables)
            {
                // TODO - need to modify is culled to handle billboard matrix offset
                //if (IsCulled(drawable.GetBoundingBox(), ModelMatrixStack.Peek())) continue;

                var billboardMatrix = billboard.ComputeMatrix(modelView, eyeLocal);

                var drawablePso = pso;
                if (drawable.HasPipelineState)
                {
                    drawablePso = drawable.PipelineState;
                }

                //
                // This allocates / updates vbo/ibos
                //
                drawable.ConfigureDeviceBuffers(GraphicsDevice);

                var renderElementCache = new Dictionary<IRenderGroupState, RenderGroupElement>();

                foreach (var pset in drawable.PrimitiveSets)
                {
                    // TODO - need to modify is culled to handle billboard matrix offset
                    //if (IsCulled(pset.GetBoundingBox(), ModelMatrixStack.Peek())) continue;

                    //            
                    // Sort into appropriate render group
                    // 
                    IRenderGroupState renderGroupState = null;
                    if (drawablePso.BlendStateDescription.AttachmentStates.Contains(BlendAttachmentDescription.AlphaBlend))
                    {
                        renderGroupState = TransparentRenderGroup.GetOrCreateState(GraphicsDevice, drawablePso, pset.PrimitiveType, drawable.VertexLayout);
                    }
                    else
                    {
                        renderGroupState = OpaqueRenderGroup.GetOrCreateState(GraphicsDevice, drawablePso, pset.PrimitiveType, drawable.VertexLayout);
                    }

                    if (false == renderElementCache.TryGetValue(renderGroupState, out var renderElement))
                    {
                        renderElement = new RenderGroupElement()
                        {
                            ModelViewMatrix = billboardMatrix.PostMultiply(modelView),
                            VertexBuffer = drawable.GetVertexBufferForDevice(GraphicsDevice),
                            IndexBuffer = drawable.GetIndexBufferForDevice(GraphicsDevice),
                            PrimitiveSets = new List<IPrimitiveSet>()
                        };
                        renderGroupState.Elements.Add(renderElement);

                        renderElementCache.Add(renderGroupState, renderElement);
                    }
                    renderElement.PrimitiveSets.Add(pset);
                }
            }
        }*/
    }
}