namespace CasaEngine.Framework.SceneManagement;

public class UpdateVisitor : NodeVisitor, IUpdateVisitor
{
    public UpdateVisitor() : base(VisitorType.UpdateVisitor, TraversalModeType.TraverseAllChildren)
    {
    }

    public override void Apply(INode node)
    {
        HandleCallbacksAndTraverse(node);
    }

    public override void Apply(IGeode geode)
    {
        base.Apply(geode);
    }

    public override void Apply(ITransform transform)
    {
        base.Apply(transform);
    }
    /*
    public override void Apply(IBillboard billboard)
    {
        base.Apply(billboard);
    }
    */
    protected void HandleCallbacks(IPipelineState state)
    {
        // TODO Handle state updates.
    }

    protected void HandleCallbacksAndTraverse(INode node)
    {
        var updateCallback = node.GetUpdateCallback();
        updateCallback?.Invoke(this, node);

        Traverse(node);
    }
}