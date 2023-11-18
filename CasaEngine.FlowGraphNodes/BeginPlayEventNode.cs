using FlowGraph.Attributes;
using FlowGraph.Nodes;

namespace CasaEngine.FlowGraphNodes;

[Name("Begin Play")]
public class BeginPlayEventNode : EventNode
{
#if EDITOR
    public BeginPlayEventNode()
    {
    }

    public override SequenceNode Copy()
    {
        return new BeginPlayEventNode();
    }
#endif

    public override string Title => "Begin Play";

    protected override void InitializeSlots()
    {
        base.InitializeSlots();

        AddSlot(0, "", SlotType.NodeOut);
    }

    public override SequenceNode Copy()
    {
        return new BeginPlayEventNode();
    }

    protected override void TriggeredImpl(object? para)
    {
    }
}