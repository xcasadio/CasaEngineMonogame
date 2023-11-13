using System.Windows.Controls;
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
        }
    }
}
