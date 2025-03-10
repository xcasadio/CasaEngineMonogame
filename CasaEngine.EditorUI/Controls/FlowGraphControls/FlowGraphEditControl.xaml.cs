﻿using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Log;
using CasaEngine.DotNetCompiler;
using CasaEngine.DotNetCompiler.CSharp;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Scripting;
using FlowGraphUI;

namespace CasaEngine.EditorUI.Controls.FlowGraphControls;

public partial class FlowGraphEditControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(EntityViewModel), typeof(FlowGraphEditControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnComponentPropertyChanged, null));

    public Entity Entity { get; private set; }

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
            var entity = (entityViewModel.Entity);
            flowGraphEditControl.Entity = entity;
            flowGraphEditControl.DataContext = new FlowGraphViewModel(entity.FlowGraph);
        }
    }

    private void ButtonCompile_OnClick(object sender, RoutedEventArgs e)
    {
        CompileFlowGraph();
    }

    private bool CompileFlowGraph()
    {
        var classSourceCode = FlowGraphToCSharp.GenerateClassCode(Entity.FlowGraph);

        var controller = new CSharpDynamicScriptController(new ClassCodeTemplate());
        var result = controller.Evaluate(new DotNetDynamicScriptParameter(
            classSourceCode,
            $"flowGraphFrom_{Entity.Name}",
            new List<string> { "CasaEngine.Core.Maths", "CasaEngine.Framework.Entities", "CasaEngine.Framework.Entities.Components" },
            new List<string> { "CasaEngine.dll", "MonoGame.Framework.dll" }));

        var namespaceName = "myNameSpace";
        var className = "className";

        //just check if everything is OK
        var gameplayProxy = (GameplayProxy)controller.CreateInstance(namespaceName, className);

        //TODo : sauvegarder le fichier .cs, the Dll file ??
        //Entity.GameplayProxy = gameplayProxy;
        Entity.GameplayProxyClassName = className;

        return result.Success;
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (Entity == null)
        {
            return;
        }

        var fileName = Entity.FileName;
        AssetSaver.SaveAsset(fileName, Entity);
        Logs.WriteInfo($"Entity {Entity.Name} saved ({fileName})");
    }
}