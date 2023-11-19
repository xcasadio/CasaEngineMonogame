using DotNetCodeGenerator;
using DotNetCodeGenerator.Ast;
using FlowGraph;
using FlowGraph.Nodes;

namespace CasaEngine.Framework.Scripting;

public class CSharpWriter
{
    private readonly CodeGenerator _codeGenerator;

    public CSharpWriter(TextWriter textWriter)
    {
        _codeGenerator = new CodeGenerator(textWriter);
    }

    public void GenerateCode(FlowGraphManager flowGraph)
    {
        var rootStatement = new Block();
        rootStatement.Statements.Add(new UsingDeclaration("System"));
        rootStatement.Statements.Add(new NamespaceDeclaration("namespace_flowgraph"));
        var classDeclaration = new ClassDeclaration("class_flowgraph");
        rootStatement.Statements.Add(classDeclaration);

        foreach (var eventNode in flowGraph.Sequence.Nodes.Where(x => x is EventNode))
        {
            classDeclaration.Body.Statements.Add(eventNode.GenerateAst());
        }

        _codeGenerator.GenerateCode(rootStatement);
    }
}