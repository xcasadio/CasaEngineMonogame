﻿using System.Xml;
using DotNetCodeGenerator.Ast;
using FlowGraph.Attributes;
using FlowGraph.Logger;
using FlowGraph.Nodes;
using FlowGraph.Process;
using Logger;

namespace CasaEngine.FlowGraphNodes;

[System.ComponentModel.Category("Action"), Name("Log")]
public class LogMessageNode : ActionNode
{
    public enum NodeSlotId
    {
        In,
        Out,
        Message
    }

    public override string Title => "Log Message";

    public LogMessageNode(XmlNode node)
        : base(node)
    {

    }

    public LogMessageNode()
    {

    }

    protected override void InitializeSlots()
    {
        base.InitializeSlots();

        AddSlot((int)NodeSlotId.In, "", SlotType.NodeIn);
        AddSlot((int)NodeSlotId.Out, "", SlotType.NodeOut);

        AddSlot((int)NodeSlotId.Message, "Message", SlotType.VarIn, typeof(string));
    }

    public override ProcessingInfo ActivateLogic(ProcessingContext context, NodeSlot slot)
    {
        var info = new ProcessingInfo
        {
            State = LogicState.Ok
        };

        var val = GetValueFromSlot((int)NodeSlotId.Message);

        if (val == null)
        {
            info.State = LogicState.Warning;
            info.ErrorMessage = "Please connect a string variable node";

            LogManager.Instance.WriteLine(LogVerbosity.Warning,
                "{0} : No message display because no variable node connected. {1}.",
                Title, info.ErrorMessage);
        }
        else
        {
            LogManager.Instance.WriteLine(LogVerbosity.Info, val.ToString()!);
        }

        ActivateOutputLink(context, (int)NodeSlotId.Out);

        return info;
    }

    public override SequenceNode Copy()
    {
        return new LogMessageNode();
    }

    public override Statement GenerateAst()
    {
        Statement message = null;
        var slot = GetSlotById((int)NodeSlotId.Message);

        if (slot.ConnectedNodes.Count > 0)
        {
            var dstSlot = slot.ConnectedNodes[0];
            var node = slot.ConnectedNodes[0].Node;

            // Connected directly to a NodeSlot value (VarOut) ?
            if (dstSlot is NodeSlotVar @var)
            {
                message = new LiteralAccessor(var.Value);
            }

            if (node is VariableNode variableNode)
            {
                message = variableNode.GenerateAst();
            }

            throw new InvalidOperationException($"Node({Id}) GetValueFromSlot({(int)NodeSlotId.Message}) : type of link not supported");
        }

        // if no node is connected, we take the nested value of the slot
        if (slot is NodeSlotVar slotVar)
        {
            message = new LiteralAccessor(slotVar.Value);
        }

        var parameters = new List<Statement> { message };

        return new FunctionCall("LogManager.Instance.WriteLineDebug", parameters);
    }
}