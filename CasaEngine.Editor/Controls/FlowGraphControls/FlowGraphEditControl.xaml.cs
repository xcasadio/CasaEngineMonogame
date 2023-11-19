using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Editor.Controls.EntityControls;
using CasaEngine.FlowGraphNodes;
using CasaEngine.Framework.Scripting;
using FlowGraph;
using FlowGraphUI;

namespace CasaEngine.Editor.Controls.FlowGraphControls
{
    public partial class FlowGraphEditControl : UserControl
    {
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(EntityViewModel), typeof(FlowGraphEditControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, null));

        public EntityViewModel SelectedItem
        {
            get => (EntityViewModel)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public FlowGraphEditControl()
        {
            InitializeComponent();

            //test
            /*var flowGraphManager = new FlowGraphManager();
            FlowGraphViewModel wm = new FlowGraphViewModel(flowGraphManager);

            flowGraphControl.DataContext = wm.SequenceViewModel;

            var entityFlowGraph = new EntityFlowGraph();

            var logMessageNode = new LogMessageNode();
            logMessageNode.SetValueInSlot((int)LogMessageNode.NodeSlotId.Message, "Voici est un message");
            entityFlowGraph.FlowGraph.Sequence.AddNode(logMessageNode);
            var tickEventNode = new TickEventNode();
            entityFlowGraph.FlowGraph.Sequence.AddNode(tickEventNode);

            tickEventNode.SlotConnectorOut.First().ConnectTo(logMessageNode.SlotConnectorIn);

            entityFlowGraph.CompileFlowGraph();*/
        }

        private static void OnComponentPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is EntityViewModel entityViewModel)
            {
                (sender as FrameworkElement).DataContext = new FlowGraphViewModel((entityViewModel.Entity as EntityFlowGraph).FlowGraph);
            }
        }
    }
}
