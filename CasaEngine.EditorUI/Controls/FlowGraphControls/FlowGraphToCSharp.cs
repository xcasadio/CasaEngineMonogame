using CSharpSyntax;
using CSharpSyntax.Printer;
using FlowGraph;
using System.IO;
using Syntax = CSharpSyntax.Syntax;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.FlowGraphNodes;
using FlowGraph.Nodes;

namespace CasaEngine.EditorUI.Controls.FlowGraphControls;

public static class FlowGraphToCSharp
{
    public static string GenerateClassCode(FlowGraphManager flowGraph)
    {
        var compilationUnit = Syntax.CompilationUnit(null, GetUsings());
        var classDeclaration = Syntax.ClassDeclaration(null, Modifiers.Public, "MyClass",
            null, new BaseListSyntax { Types = { Syntax.ParseName("GameplayProxy") } });
        compilationUnit.Members.Add(classDeclaration);

        var functionByEventType = new Dictionary<string, List<MethodDeclarationSyntax>>();

        foreach (var sequenceNode in flowGraph.Sequence.Nodes.Where(x => x is EventNode))
        {
            var typeName = sequenceNode.GetType().Name;
            if (!functionByEventType.TryGetValue(typeName, out var memberDeclarations))
            {
                memberDeclarations = new List<MethodDeclarationSyntax>();
                functionByEventType.Add(typeName, memberDeclarations);
            }

            memberDeclarations.Add((MethodDeclarationSyntax)sequenceNode.GenerateAst(classDeclaration));
        }

        CreateOverrideFunctions(classDeclaration, functionByEventType);

        foreach (var pair in functionByEventType)
        {
            foreach (var methodDeclarationSyntax in pair.Value)
            {
                classDeclaration.Members.Add(methodDeclarationSyntax);
            }
        }

        return GenerateClassCode(compilationUnit);
    }

    private static string GenerateClassCode(SyntaxNode syntaxNode)
    {
        var configuration = new CSharpSyntax.Printer.Configuration.SyntaxPrinterConfiguration();

        using var writer = new StringWriter();
        using (var printer = new SyntaxPrinter(new SyntaxWriter(writer, configuration)))
        {
            printer.Visit(syntaxNode);
        }

        return writer.GetStringBuilder().ToString();
    }

    private static IEnumerable<UsingDirectiveSyntax> GetUsings()
    {
        return new List<UsingDirectiveSyntax>
        {
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("System")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("System.Collections")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("Microsoft.Xna.Framework")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Core.Design")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Core.Logger")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Core.Maths")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Engine")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Engine.Physics")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Framework")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Framework.Assets")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Framework.Entities")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Framework.Entities.Components")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Framework.Game")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Framework.Scripting")),
            Syntax.UsingDirective((NameSyntax)Syntax.ParseName("CasaEngine.Framework.World")),
        };
    }

    private static void CreateOverrideFunctions(ClassDeclarationSyntax classDeclaration,
        Dictionary<string, List<MethodDeclarationSyntax>> functionByEventType)
    {
        var initializeWithWorldMethodDeclaration = new MethodDeclarationSyntax();
        initializeWithWorldMethodDeclaration.Modifiers = Modifiers.Public | Modifiers.Override;
        initializeWithWorldMethodDeclaration.Identifier = "InitializeWithWorld";
        initializeWithWorldMethodDeclaration.ReturnType = Syntax.ParseName("void");
        initializeWithWorldMethodDeclaration.ParameterList = Syntax.ParameterList(Syntax.Parameter(identifier: "world", type: Syntax.ParseName("World.World")));
        initializeWithWorldMethodDeclaration.Body = Syntax.Block();
        classDeclaration.Members.Add(initializeWithWorldMethodDeclaration);

        var updateMethodDeclaration = new MethodDeclarationSyntax();
        updateMethodDeclaration.Modifiers = Modifiers.Public | Modifiers.Override;
        updateMethodDeclaration.Identifier = "Update";
        updateMethodDeclaration.ReturnType = Syntax.ParseName("void");
        updateMethodDeclaration.ParameterList = Syntax.ParameterList(Syntax.Parameter(identifier: "elapsedTime", type: Syntax.ParseName("float")));
        updateMethodDeclaration.Body = Syntax.Block();
        classDeclaration.Members.Add(updateMethodDeclaration);

        var drawMethodDeclaration = new MethodDeclarationSyntax();
        drawMethodDeclaration.Modifiers = Modifiers.Public | Modifiers.Override;
        drawMethodDeclaration.Identifier = "Draw";
        drawMethodDeclaration.ReturnType = Syntax.ParseName("void");
        drawMethodDeclaration.ParameterList = Syntax.ParameterList();
        drawMethodDeclaration.Body = Syntax.Block();
        classDeclaration.Members.Add(drawMethodDeclaration);

        var onHitMethodDeclaration = new MethodDeclarationSyntax();
        onHitMethodDeclaration.Modifiers = Modifiers.Public | Modifiers.Override;
        onHitMethodDeclaration.Identifier = "OnHit";
        onHitMethodDeclaration.ReturnType = Syntax.ParseName("void");
        onHitMethodDeclaration.ParameterList = Syntax.ParameterList(Syntax.Parameter(identifier: "collision", type: Syntax.ParseName("Collision")));
        onHitMethodDeclaration.Body = Syntax.Block();
        classDeclaration.Members.Add(onHitMethodDeclaration);

        var onHitEndedMethodDeclaration = new MethodDeclarationSyntax();
        onHitEndedMethodDeclaration.Modifiers = Modifiers.Public | Modifiers.Override;
        onHitEndedMethodDeclaration.Identifier = "OnHitEnded";
        onHitEndedMethodDeclaration.ReturnType = Syntax.ParseName("void");
        onHitEndedMethodDeclaration.ParameterList = Syntax.ParameterList(Syntax.Parameter(identifier: "collision", type: Syntax.ParseName("Collision")));
        onHitEndedMethodDeclaration.Body = Syntax.Block();
        classDeclaration.Members.Add(onHitEndedMethodDeclaration);

        var onBeginPlayMethodDeclaration = new MethodDeclarationSyntax();
        onBeginPlayMethodDeclaration.Modifiers = Modifiers.Public | Modifiers.Override;
        onBeginPlayMethodDeclaration.Identifier = "OnBeginPlay";
        onBeginPlayMethodDeclaration.ReturnType = Syntax.ParseName("void");
        onBeginPlayMethodDeclaration.ParameterList = Syntax.ParameterList(Syntax.Parameter(identifier: "world", type: Syntax.ParseName("World.World")));
        onBeginPlayMethodDeclaration.Body = Syntax.Block();
        classDeclaration.Members.Add(onBeginPlayMethodDeclaration);

        var index = 1;
        foreach (var memberDeclaration in functionByEventType[nameof(BeginPlayEventNode)])
        {
            memberDeclaration.Identifier += index;
            index++;

            var parameters = Syntax.ArgumentList(
                Syntax.Argument((SimpleNameSyntax)Syntax.ParseName("world")));

            var invocationExpressionSyntax = new InvocationExpressionSyntax
            {
                Expression = Syntax.ParseName(memberDeclaration.Identifier),
                ArgumentList = parameters
            };

            onBeginPlayMethodDeclaration.Body.Statements.Add(Syntax.ExpressionStatement(invocationExpressionSyntax));
        }

        var onEndPlayMethodDeclaration = new MethodDeclarationSyntax();
        onEndPlayMethodDeclaration.Modifiers = Modifiers.Public | Modifiers.Override;
        onEndPlayMethodDeclaration.Identifier = "OnEndPlay";
        onEndPlayMethodDeclaration.ReturnType = Syntax.ParseName("void");
        onEndPlayMethodDeclaration.ParameterList = Syntax.ParameterList(Syntax.Parameter(identifier: "world", type: Syntax.ParseName("World.World")));
        onEndPlayMethodDeclaration.Body = Syntax.Block();
        classDeclaration.Members.Add(onEndPlayMethodDeclaration);

        var cloneMethodDeclaration = new MethodDeclarationSyntax();
        cloneMethodDeclaration.Modifiers = Modifiers.Public | Modifiers.Override;
        cloneMethodDeclaration.Identifier = "Clone";
        cloneMethodDeclaration.ParameterList = Syntax.ParameterList();
        cloneMethodDeclaration.ReturnType = Syntax.ParseName("GameplayProxy");
        cloneMethodDeclaration.Body = Syntax.Block();
        cloneMethodDeclaration.Body.Statements.Add(
            Syntax.ReturnStatement(Syntax.ObjectCreationExpression(
                Syntax.ParseName(classDeclaration.Identifier), Syntax.ArgumentList())));
        classDeclaration.Members.Add(cloneMethodDeclaration);
    }


    private static readonly string CodeTemplate = @"using System;
using System.Collections;
using System.Text.Json;
using Microsoft.Xna.Framework;

using CasaEngine.Core.Design;
using CasaEngine.Core.Logger;
using CasaEngine.Core.Maths;
using CasaEngine.Engine;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;

namespace generatedScope;

public class GeneratedClass : GameplayProxy
{
    public override void InitializeWithWorld(World.World world) 
	{
		//nothing
	}

    public override void Update(float elapsedTime)
	{
		//if node
	}
	
    public override void Draw()
	{
		//if node
	}

    public override void OnHit(Collision collision)
	{
		//if node
	}
	
    public override void OnHitEnded(Collision collision)
	{
		//if node
	}
	
    public override void OnBeginPlay(World.World world)
	{
		//if node
	}
	
    public override void OnEndPlay(World.World world)
	{
		//if node
	}

    public override GameplayProxy Clone()
	{
		return GeneratedClass();
	}
}";
}