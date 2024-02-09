using System.Collections;
using CSharpSyntax;
using CSharpSyntax.Printer;
using FlowGraph;
using System.IO;
using Syntax = CSharpSyntax.Syntax;
using System.Collections.Generic;
using System.Linq;
using FlowGraph.Nodes;

namespace CasaEngine.EditorUI.Controls.FlowGraphControls;

public static class FlowGraphToCSharp
{
    private const string MethodsToken = "##GeneratedMethods##";

    public static string GenerateClassCode(FlowGraphManager flowGraph)
    {
        var compilationUnit = Syntax.CompilationUnit(null, GetUsings());
        var classDeclaration = Syntax.ClassDeclaration(null, Modifiers.Public, "MyClass",
            null, new BaseListSyntax { Types = { Syntax.ParseName("GameplayProxy") } });

        foreach (var sequenceNode in flowGraph.Sequence.Nodes.Where(x => x is EventNode))
        {
            classDeclaration.Members.Add((MemberDeclarationSyntax)sequenceNode.GenerateAst());

        }

        compilationUnit.Members.Add(classDeclaration);

        /*
        Syntax.MethodDeclaration(
            identifier: "Method",
            returnType: Syntax.ParseName("void"),
            modifiers: Modifiers.Public,
            parameterList: Syntax.ParameterList(),
            body: Syntax.Block()
        )*/

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
    public Entity Entity;

    public override void LoadContent(Entity entity)
    {
        Entity = entity;
    }

    public override void Update(float elapsedTime)
    {
        ##TickEvent##
    }

    public override void Draw()
    {
    }

    public override void OnHit(Collision collision)
    {
        ##OnHit##
    }

    public override void OnHitEnded(Collision collision)
    {
        ##OnHitEnded##
    }

    public override void OnBeginPlay(World world)
    {
        ##OnBeginPlay##
    }

    public override void OnEndPlay(World world)
    {
        ##OnEndPlay##
    }

    " + MethodsToken + @"
}";
}