using FlowGraph.Attributes;
using FlowGraph.Nodes;

namespace CasaEngine.FlowGraphNodes;

[Name("End Play")]
public class EndPlayEventNode : EventNode
{
#if EDITOR
    public EndPlayEventNode()
    {
    }

    protected override SequenceNode CopyImpl()
    {
        return new EndPlayEventNode();
    }
#endif

    public override string Title => "End Play";

    protected override void InitializeSlots()
    {
        base.InitializeSlots();

        AddSlot(0, "", SlotType.NodeOut);
    }

    protected override SequenceNode CopyImpl()
    {
        return new EndPlayEventNode();
    }

    protected override void TriggeredImpl(object? para)
    {
    }
}