using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Veldrid.SceneGraph.RenderGraph;

namespace Veldrid.SceneGraph.Util;

internal class Renderer //: IGraphicsDeviceOperation
{
    private IUpdateVisitor _updateVisitor;
    private ICullVisitor _cullVisitor;
    /*
    private DeviceBuffer _projectionBuffer;
    private DeviceBuffer _viewBuffer;
    private CommandList _commandList;
    */
    private bool _initialized = false;

    private RenderInfo _renderInfo;

    private Stopwatch _stopWatch = new Stopwatch();

    public Renderer()
    {
        _updateVisitor = new UpdateVisitor();
        _cullVisitor = new CullVisitor();
    }

    public void Initialize(GraphicsDevice device)
    {
        _cullVisitor.GraphicsDevice = device;
        /*
        _renderInfo = new RenderInfo();
        _renderInfo.GraphicsDevice = device;
        _renderInfo.CommandList = _commandList;
        */
        _initialized = true;
    }

    private void Update(IGroup sceneRootNode)
    {
        sceneRootNode.Accept(_updateVisitor);
    }

    private void Cull(GraphicsDevice device, IGroup sceneRootNode, Matrix viewMatrix, Matrix projectionMatrix)
    {
        _cullVisitor.Reset();

        _cullVisitor.SetViewMatrix(viewMatrix);
        _cullVisitor.SetProjectionMatrix(projectionMatrix);

        _cullVisitor.Prepare();

        sceneRootNode.Accept(_cullVisitor);
    }

    private void Record(GraphicsDevice device)
    {
        if (!_initialized)
        {
            Initialize(device);
        }
        /*
        _commandList.Begin();

        // We want to render directly to the output window.
        _commandList.SetFramebuffer(Framebuffer);

        // TODO Set from Camera color ?
        _commandList.ClearColorTarget(0, RgbaFloat.Grey);
        _commandList.ClearDepthStencil(1f);

        DrawOpaqueRenderGroups(device);

        if (_cullVisitor.TransparentRenderGroup.HasDrawableElements())
        {
            DrawTransparentRenderGroups(device);
        }

        _commandList.End();*/
    }

    private void Draw(GraphicsDevice device)
    {/*
        device.SubmitCommands(_commandList, _fence);
        device.WaitForFence(_fence);
        device.WaitForIdle();*/
    }

    private void DrawOpaqueRenderGroups(GraphicsDevice device)
    {/*
        var alignment = device.UniformBufferMinOffsetAlignment;
        var modelViewMatrixObjSizeInBytes = 64u;
        var hostBuffStride = 1u;
        if (alignment > 64u)
        {
            hostBuffStride = alignment / 64u;
            modelViewMatrixObjSizeInBytes = alignment;
        }

        foreach (var state in _cullVisitor.OpaqueRenderGroup.GetStateList())
        {
            var ri = state.GetPipelineAndResources(device);

            _commandList.SetPipeline(ri.GraphicsDevice);

            var nDrawables = (uint)state.Elements.Count;
            var modelMatrixViewBuffer = new Matrix[nDrawables * hostBuffStride]; // TODO - do we need to allocate this every frame?
            for (var i = 0; i < nDrawables; ++i)
            {
                modelMatrixViewBuffer[i * hostBuffStride] = state.Elements[i].ModelViewMatrix;
            }
            _commandList.UpdateBuffer(ri.ModelViewBuffer, 0, modelMatrixViewBuffer);

            for (var i = 0; i < nDrawables; ++i)
            {
                var element = state.Elements[i];
                var offsetsList = new List<uint>();

                foreach (var stride in ri.UniformStrides)
                {
                    offsetsList.Add((uint)i * stride);
                }

                var offsets = offsetsList.ToArray();

                _commandList.SetVertexBuffer(0, element.VertexBuffer);

                _commandList.SetIndexBuffer(element.IndexBuffer, IndexFormat.UInt32);

                _commandList.SetGraphicsResourceSet(0, _resourceSet);

                _commandList.SetGraphicsResourceSet(1, ri.ResourceSet, offsets);

                foreach (var primitiveSet in element.PrimitiveSets)
                {
                    primitiveSet.Draw(_commandList);
                }
            }
        }*/
    }

    private void DrawTransparentRenderGroups(GraphicsDevice device)
    {/*
        var alignment = device.UniformBufferMinOffsetAlignment;
        var modelBuffStride = 64u;
        var hostBuffStride = 1u;
        if (alignment > 64u)
        {
            hostBuffStride = alignment / 64u;
            modelBuffStride = alignment;
        }

        //
        // First sort the transparent render elements by distance to eye point (if not culled).
        //
        var drawOrderMap = new SortedList<float, List<Tuple<IRenderGroupState, RenderGroupElement, IPrimitiveSet, uint>>>();
        drawOrderMap.Capacity = _cullVisitor.RenderElementCount;
        var transparentRenderGroupStates = _cullVisitor.TransparentRenderGroup.GetStateList();

        var stateToUniformDict = new Dictionary<IRenderGroupState, Matrix[]>();

        foreach (var state in transparentRenderGroupStates)
        {
            var nDrawables = (uint)state.Elements.Count;
            var modelMatrixViewBuffer = new Matrix[nDrawables * hostBuffStride];

            // Iterate over all elements in this state
            for (var j = 0; j < nDrawables; ++j)
            {
                var renderElement = state.Elements[j];
                modelMatrixViewBuffer[j * hostBuffStride] = state.Elements[j].ModelViewMatrix;

                // Iterate over all primitive sets in this state
                foreach (var pset in renderElement.PrimitiveSets)
                {
                    var ctr = pset.GetBoundingBox().GetCenter();

                    // Compute distance eye point 
                    var modelView = renderElement.ModelViewMatrix;
                    var ctr_w = Vector3.Transform(ctr, modelView);
                    var dist = Vector3.Distance(ctr_w, Vector3.Zero);

                    if (!drawOrderMap.TryGetValue(dist, out var renderList))
                    {
                        renderList = new List<Tuple<IRenderGroupState, RenderGroupElement, IPrimitiveSet, uint>>();
                        drawOrderMap.Add(dist, renderList);
                    }

                    renderList.Add(Tuple.Create(state, renderElement, pset, (uint)j));
                }
            }
            stateToUniformDict.Add(state, modelMatrixViewBuffer);
        }

        DeviceBuffer boundVertexBuffer = null;
        DeviceBuffer boundIndexBuffer = null;

        // Now draw transparent elements, back to front
        IRenderGroupState lastState = null;
        RenderGraph.RenderInfo ri = null;

        var currModelViewMatrix = Matrix.Identity;

        foreach (var renderList in drawOrderMap.Reverse())
        {
            foreach (var element in renderList.Value)
            {
                var state = element.Item1;

                if (null == lastState || state != lastState)
                {
                    ri = state.GetPipelineAndResources(device);

                    // Set this state's pipeline
                    _commandList.SetPipeline(ri.GraphicsDevice);

                    _commandList.UpdateBuffer(ri.ModelViewBuffer, 0, stateToUniformDict[state]);

                    // Set the resources
                    _commandList.SetGraphicsResourceSet(0, _resourceSet);
                }

                uint offset = element.Item4 * modelBuffStride;

                // Set state-local resources
                _commandList.SetGraphicsResourceSet(1, ri.ResourceSet, 1, ref offset);

                var renderGroupElement = element.Item2;

                if (boundVertexBuffer != renderGroupElement.VertexBuffer)
                {
                    // Set vertex buffer
                    _commandList.SetVertexBuffer(0, renderGroupElement.VertexBuffer);
                    boundVertexBuffer = renderGroupElement.VertexBuffer;
                }

                if (boundIndexBuffer != renderGroupElement.IndexBuffer)
                {
                    // Set index buffer
                    _commandList.SetIndexBuffer(renderGroupElement.IndexBuffer, IndexFormat.UInt32);
                    boundIndexBuffer = renderGroupElement.IndexBuffer;
                }

                element.Item3.Draw(_commandList);

                lastState = state;
            }
        }*/
    }

    private void UpdateUniforms(GraphicsDevice device, Matrix viewMatrix, Matrix projectionMatrix)
    {
        /*
        device.UpdateBuffer(_projectionBuffer, 0, projectionMatrix);

        // TODO - Remove
        device.UpdateBuffer(_viewBuffer, 0, viewMatrix); //Matrix.Identity);
        */
    }

    public void HandleOperation(GraphicsDevice device, IGroup sceneRootNode, Matrix viewMatrix, Matrix projectionMatrix)
    {
        _stopWatch.Reset();
        _stopWatch.Start();

        Update(sceneRootNode);

        UpdateUniforms(device, viewMatrix, projectionMatrix);

        var postUpdate = _stopWatch.ElapsedMilliseconds;

        Cull(device, sceneRootNode, viewMatrix, projectionMatrix);

        var postCull = _stopWatch.ElapsedMilliseconds;

        Record(device);

        var postRecord = _stopWatch.ElapsedMilliseconds;

        Draw(device);

        var postDraw = _stopWatch.ElapsedMilliseconds;

        //SwapBuffers(device);

        var postSwap = _stopWatch.ElapsedMilliseconds;

        /*_logger.LogTrace(string.Format("Update = {0} ms, Cull = {1} ms, Record = {2}, Draw = {3} ms, Swap = {4} ms",
            postUpdate,
            postCull - postUpdate,
            postRecord - postCull,
            postDraw - postRecord,
            postSwap - postDraw));*/
    }
}