using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.FlowGraphNodes;
using DotNetCodeGenerator;
using DotNetCodeGenerator.Ast;
using FlowGraph;
using FlowGraph.Nodes;

namespace CasaEngine.EditorUI.Controls.FlowGraphControls;

public static class FlowGraphToCSharp
{
    private const string MethodsToken = "##GeneratedMethods##";

    public static GeneratedClassInformations GenerateClassCode(FlowGraphManager flowGraph)
    {
        var dotNetWriter = new CSharpWriter();
        /*
        var methodsAstByEventNode = flowGraph.Sequence.Nodes
            .Where(x => x is EventNode)
            .ToDictionary(x => x, x => x.GenerateAst());

        var methodsStatement = new List<Statement>
        {
            new PropertyDeclaration(nameof(Int32), "ExternalComponentId"),
            GenerateUpdateFunction(methodsAstByEventNode),
            //GenerateOnHitFunction(eventNode),
            //GenerateOnHitEndedFunction(eventNode),
            GenerateOnBeginPlayFunction(methodsAstByEventNode),
            GenerateOnEndPlayFunction(methodsAstByEventNode)
        };
        
        methodsStatement.AddRange(methodsAstByEventNode.Select(statement => statement.Value));

        return dotNetWriter.GenerateClassCode(methodsStatement);
        */

        var StatementByNodeType = new Dictionary<string, List<Statement>>();

        foreach (var sequenceNode in flowGraph.Sequence.Nodes.Where(x => x is EventNode))
        {
            if (!StatementByNodeType.TryGetValue(sequenceNode.GetType().FullName, out var statements))
            {
                statements = new List<Statement>();
                StatementByNodeType.Add(sequenceNode.GetType().FullName, statements);
            }

            statements.Add(sequenceNode.GenerateAst());
        }

        var functionCode = dotNetWriter.GenerateClassCode2(StatementByNodeType.SelectMany(x => x.Value));

        var generatedCode = CodeTemplate.Replace(MethodsToken, functionCode);

        generatedCode = GenerateFunctionCalls(generatedCode, "##TickEvent##", StatementByNodeType, "elapsedTime", typeof(TickEventNode));
        generatedCode = generatedCode.Replace("##OnHit##", string.Empty).Replace("##OnHitEnded##", string.Empty);
        //generatedCode = GenerateFunctionCalls(generatedCode, "##OnHit##", StatementByNodeType, "collision", typeof(HitEventNode));
        //generatedCode = GenerateFunctionCalls(generatedCode, "##OnHitEnded##", StatementByNodeType, "collision", typeof(HitEndedEventNode));
        generatedCode = GenerateFunctionCalls(generatedCode, "##OnBeginPlay##", StatementByNodeType, "world", typeof(BeginPlayEventNode));
        generatedCode = GenerateFunctionCalls(generatedCode, "##OnEndPlay##", StatementByNodeType, "world", typeof(EndPlayEventNode));

        return new GeneratedClassInformations
        {
            Namespace = "generatedScope",
            ClassName = "GeneratedClass",
            Code = generatedCode
        };
    }

    private static string GenerateFunctionCalls(string code, string token, Dictionary<string, List<Statement>> statementByNodeType, string parameters, Type type)
    {
        string value = string.Empty;
        if (statementByNodeType.TryGetValue(type.FullName, out var statements)
            && statements.Count >= 0)
        {
            value = string.Join(Environment.NewLine,
                statements.Select(x => $"{((FunctionDeclaration)x).Name.Literal}({parameters});{Environment.NewLine}"));
        }

        return code.Replace(token, value);
    }

    private static Statement GenerateUpdateFunction(IDictionary<SequenceNode, Statement> methodsAstByNode)
    {
        var elapsedTimeToken = new Token(TokenType.Var, "float", "elapsedTime");
        var parameters = new List<Token> { elapsedTimeToken };
        var functionDeclaration = new FunctionDeclaration("Update", parameters) { IsOverride = true };

        foreach (var nodeWithAst in methodsAstByNode.Where(x => x.Key.GetType().FullName == typeof(TickEventNode).FullName))
        {
            var methodAst = nodeWithAst.Value;
            var variableStatement = new LiteralAccessor(elapsedTimeToken);
            var functionCall = new FunctionCall((methodAst as FunctionDeclaration).Name.Literal.ToString(), new[] { variableStatement });
            functionDeclaration.Body.Statements.Add(functionCall);
        }

        return functionDeclaration;
    }
    /*
    private static Statement GenerateOnHitFunction(List<SequenceNode> nodes)
    {
        var parameters = new List<Token> { new Token(TokenType.Var, "Collision", "collision") };
        var functionDeclaration = new FunctionDeclaration("OnHit", parameters) { IsOverride = true };

        foreach (var tickEventNode in nodes.Where(x => x is OnHitEventNode))
        {
            functionDeclaration.Body.Statements.Add(tickEventNode.GenerateAst());
        }

        return functionDeclaration;
    }

    private static Statement GenerateOnHitEndedFunction(List<SequenceNode> nodes)
    {
        var parameters = new List<Token> { new Token(TokenType.Var, "Collision", "collision") };
        var functionDeclaration = new FunctionDeclaration("OnHitEnded", parameters) { IsOverride = true };

        foreach (var tickEventNode in nodes.Where(x => x is OnHitEndedEventNode))
        {
            functionDeclaration.Body.Statements.Add(tickEventNode.GenerateAst());
        }

        return functionDeclaration;
    }
    */
    private static Statement GenerateOnBeginPlayFunction(IDictionary<SequenceNode, Statement> methodsAstByNode)
    {
        var worldToken = new Token(TokenType.Var, "World", "world");
        var parameters = new List<Token> { worldToken };
        var functionDeclaration = new FunctionDeclaration("OnBeginPlay", parameters) { IsOverride = true };

        foreach (var nodeWithAst in methodsAstByNode.Where(x => x.Key is TickEventNode))
        {
            var methodAst = nodeWithAst.Value;
            var variableStatement = new LiteralAccessor(worldToken);
            var functionCall = new FunctionCall((methodAst as FunctionDeclaration).Name.Literal.ToString(), new[] { variableStatement });
            functionDeclaration.Body.Statements.Add(functionCall);
        }

        return functionDeclaration;
    }

    private static Statement GenerateOnEndPlayFunction(IDictionary<SequenceNode, Statement> methodsAstByNode)
    {
        var worldToken = new Token(TokenType.Var, "World", "world");
        var parameters = new List<Token> { worldToken };
        var functionDeclaration = new FunctionDeclaration("OnEndPlay", parameters) { IsOverride = true };

        foreach (var nodeWithAst in methodsAstByNode.Where(x => x.Key is TickEventNode))
        {
            var methodAst = nodeWithAst.Value;
            var variableStatement = new LiteralAccessor(worldToken);
            var functionCall = new FunctionCall((methodAst as FunctionDeclaration).Name.Literal.ToString(), new[] { variableStatement });
            functionDeclaration.Body.Statements.Add(functionCall);
        }

        return functionDeclaration;
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
    public override int ExternalComponentId => -1;
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

    public override void Load(JsonElement element)
    {

    }

    " + MethodsToken + @"
}";
}