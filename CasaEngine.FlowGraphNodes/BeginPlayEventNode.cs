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

    protected override SequenceNode CopyImpl()
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

    protected override SequenceNode CopyImpl()
    {
        return new BeginPlayEventNode();
    }

    protected override void TriggeredImpl(object? para)
    {
    }
}