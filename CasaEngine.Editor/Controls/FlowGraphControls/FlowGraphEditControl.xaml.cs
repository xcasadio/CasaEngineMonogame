using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.DotNetCompiler;
using CasaEngine.DotNetCompiler.CSharp;
using CasaEngine.Editor.Controls.EntityControls;
using CasaEngine.Framework.Scripting;
using FlowGraphUI;

namespace CasaEngine.Editor.Controls.FlowGraphControls;

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
        CompileFlowGraph();
    }

    private bool CompileFlowGraph()
    {
        var generatedClassInformations = FlowGraphToCSharp.GenerateClassCode(EntityFlowGraph.FlowGraph);

        var controller = new CSharpDynamicScriptController(new ClassCodeTemplate());
        var result = controller.Evaluate(new DotNetDynamicScriptParameter(
            generatedClassInformations.Code,
            $"flowGraphFrom_{EntityFlowGraph.Name}",
            new List<string> { "CasaEngine.Core.Maths", "CasaEngine.Framework.Entities", "CasaEngine.Framework.Entities.Components" },
            new List<string> { "CasaEngine.dll", "MonoGame.Framework.dll" }));

        var externalComponent = (ExternalComponent)controller.CreateInstance(
            generatedClassInformations.Namespace,
            generatedClassInformations.ClassName);

        //sauvegarder le fichier .cs
        EntityFlowGraph.InitializeScript(externalComponent);

        return result.Success;
    }
}