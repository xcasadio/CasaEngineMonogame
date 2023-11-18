using DotNetCodeGenerator;
using DotNetCodeGenerator.Ast;
using FlowGraph;
using FlowGraph.Nodes;

namespace CasaEngine.Framework.Scripting;

public class CSharpWriter
{
    private int _indentLevel = 0;
    private readonly TextWriter _textWriter;
    private readonly CodeGenerator _codeGenerator;

    public CSharpWriter(TextWriter textWriter)
    {
        _textWriter = textWriter;
        _codeGenerator = new CodeGenerator(_textWriter);
    }

    public void GenerateCode(FlowGraphManager flowGraph)
    {
        var rootStatement = new Block();
        rootStatement.Statements.Add(new UsingDeclaration("System"));
        rootStatement.Statements.Add(new NamespaceDeclaration("namespace_generated_code"));
        var classDeclaration = new ClassDeclaration("class_from_flowgraph");
        rootStatement.Statements.Add(classDeclaration);

        foreach (var eventNode in flowGraph.Sequence.Nodes.Where(x => x is EventNode))
        {
            classDeclaration.Body.Statements.Add(eventNode.GenerateAst());
        }

        _codeGenerator.GenerateCode(rootStatement);
    }
}