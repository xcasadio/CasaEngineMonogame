using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public class CullVisitor : NodeVisitor, ICullVisitor
{
    public List<Tuple<StaticMesh, Matrix>> Meshes = new();
    private Polytope CullingFrustum { get; } = new();
    private Stack<Matrix> ModelMatrixStack { get; set; } = new();

    public CullVisitor() :
        base(VisitorType.CullAndAssembleVisitor, TraversalModeType.TraverseActiveChildren)
    {
        ModelMatrixStack.Push(Matrix.Identity);
    }

    public void Reset()
    {
        Meshes.Clear();

        ModelMatrixStack.Clear();
        ModelMatrixStack.Push(Matrix.Identity);
    }

    public void SetView(Matrix viewMatrix, Matrix projectionMatrix)
    {
        CullingFrustum.SetViewProjectionMatrix(viewMatrix * projectionMatrix);
    }

    private bool IsCulled(BoundingBox bb, Matrix modelMatrix)
    {
        var culled = !CullingFrustum.Contains(bb, modelMatrix);

        return culled;
    }

    public override void Apply(INode node)
    {
        Traverse(node);
    }

    public override void Apply(ITransform transform)
    {
        var curModel = ModelMatrixStack.Peek();
        transform.ComputeLocalToWorldMatrix(ref curModel, this);
        ModelMatrixStack.Push(curModel);

        Apply((INode)transform);

        ModelMatrixStack.Pop();
    }

    public override void Apply(IGeode geode)
    {
        var boundingBox = geode.GetBoundingBox();
        var modelMatrix = ModelMatrixStack.Peek();

        if (IsCulled(boundingBox, modelMatrix))
        {
            return;
        }

        foreach (var drawable in geode.Drawables)
        {
            if (IsCulled(drawable.GetBoundingBox(), modelMatrix))
            {
                continue;
            }

            Meshes.Add(new Tuple<StaticMesh, Matrix>(drawable.Mesh, modelMatrix));
        }
    }
}