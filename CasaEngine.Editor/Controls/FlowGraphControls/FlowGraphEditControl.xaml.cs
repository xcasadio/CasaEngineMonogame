using System.Linq;
using System.Windows.Controls;
using CasaEngine.FlowGraphNodes;
using CasaEngine.Framework.Scripting;
using FlowGraph;
using FlowGraphUI;

namespace CasaEngine.Editor.Controls.FlowGraphControls
{
    public partial class FlowGraphEditControl : UserControl
    {
        public FlowGraphEditControl()
        {
            InitializeComponent();

            //test
            var flowGraphManager = new FlowGraphManager();
            FlowGraphViewModel wm = new FlowGraphViewModel(flowGraphManager);

            flowGraphControl.DataContext = wm.SequenceViewModel;

            var entityFlowGraph = new EntityFlowGraph();

            var logMessageNode = new LogMessageNode();
            logMessageNode.SetValueInSlot((int)LogMessageNode.NodeSlotId.Message, "Ceci est un message");
            entityFlowGraph.FlowGraph.Sequence.AddNode(logMessageNode);
            var tickEventNode = new TickEventNode();
            entityFlowGraph.FlowGraph.Sequence.AddNode(tickEventNode);

            tickEventNode.SlotConnectorOut.First().ConnectTo(logMessageNode.SlotConnectorIn);

            entityFlowGraph.CompileFlowGraph();
        }
    }
}
