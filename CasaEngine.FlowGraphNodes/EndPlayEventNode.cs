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

    public override SequenceNode Copy()
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

    public override SequenceNode Copy()
    {
        return new EndPlayEventNode();
    }

    protected override void TriggeredImpl(object? para)
    {
    }
}