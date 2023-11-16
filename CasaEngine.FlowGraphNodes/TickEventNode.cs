using FlowGraph.Attributes;
using FlowGraph.Nodes;

namespace CasaEngine.FlowGraphNodes;

[Name("Tick")]
public class TickEventNode : EventNode
{
    public TickEventNode()
    {
        AddSlot(0, "", SlotType.NodeOut);
        AddSlot(1, "elapsed seconds", SlotType.VarOut, typeof(float));
    }

    public override string Title => "Tick";

    protected override SequenceNode CopyImpl()
    {
        return new TickEventNode();
    }

    protected override void TriggeredImpl(object? para)
    {
        SetValueInSlot(1, para);
    }
}