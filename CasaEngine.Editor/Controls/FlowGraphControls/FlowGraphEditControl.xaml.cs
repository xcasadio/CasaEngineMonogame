using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.DotNetCompiler;
using CasaEngine.DotNetCompiler.CSharp;
using CasaEngine.Editor.Controls.EntityControls;
using CasaEngine.FlowGraphNodes;
using CasaEngine.Framework.Scripting;
using DotNetCodeGenerator;
using FlowGraph;
using FlowGraph.Nodes;
using FlowGraphUI;

namespace CasaEngine.Editor.Controls.FlowGraphControls
{
    public partial class FlowGraphEditControl : UserControl
    {
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(EntityViewModel), typeof(FlowGraphEditControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, null));

        public EntityFlowGraph EntityFlowGraph { get; private set; }

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
                var flowGraphEditControl = (sender as FlowGraphEditControl);
                var entityFlowGraph = (entityViewModel.Entity as EntityFlowGraph);
                flowGraphEditControl.DataContext = new FlowGraphViewModel(entityFlowGraph.FlowGraph);
                flowGraphEditControl.EntityFlowGraph = entityFlowGraph;
            }
        }


        private void ButtonCompile_OnClick(object sender, RoutedEventArgs e)
        {
            //var flowGraphViewModel = DataContext as FlowGraphViewModel;
            CompileFlowGraph();
        }

        private bool CompileFlowGraph()
        {
            var stream = new StringWriter();
            var dotNetWriter = new CSharpWriter(stream);

            var methodsAst = EntityFlowGraph.FlowGraph.Sequence.Nodes
                .Where(x => x is EventNode)
                .Select(x => x.GenerateAst());
            //.Concat(FlowGraph.Functions.Select(x => x.Nodes.Where(y => y is CallFunctionNode)))

            dotNetWriter.GenerateClassCode(methodsAst);

            var controller = new CSharpDynamicScriptController(new ClassCodeTemplate());
            var result = controller.Evaluate(new DotNetDynamicScriptParameter(stream.ToString()));

            EntityFlowGraph.InstanciatedObject = (ExternalComponent)controller.CreateInstance();

            /*var executionResult = controller.Execute(
                new DotNetCallArguments(namespaceName: "Test", className: "TestClass", methodName: "Run"),
                new List<ParameterArgument>() { });*/

            return result.Success;
        }
    }
}
