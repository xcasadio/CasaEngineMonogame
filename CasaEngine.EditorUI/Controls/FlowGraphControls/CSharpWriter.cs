using System.Collections.Generic;
using System.IO;
using CasaEngine.Framework.Scripting;
using DotNetCodeGenerator;
using DotNetCodeGenerator.Ast;

namespace CasaEngine.EditorUI.Controls.FlowGraphControls;

public class CSharpWriter
{

    private readonly CodeGenerator _codeGenerator;
    private readonly StringWriter _stream;

    public CSharpWriter()
    {
        _stream = new StringWriter();
        _codeGenerator = new CodeGenerator(_stream);
    }

    public GeneratedClassInformations GenerateClassCode(IEnumerable<Statement> methodsStatement)
    {
        var rootStatement = new Block();
        rootStatement.Statements.Add(new UsingDeclaration("System"));
        rootStatement.Statements.Add(new UsingDeclaration("System.Collections"));
        rootStatement.Statements.Add(new UsingDeclaration("Microsoft.Xna.Framework"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Core.Design"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Core.Logger"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Core.Maths"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Engine"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Framework"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Framework.Assets"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Framework.Entities"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Framework.Entities.Components"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Framework.Game"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Framework.Scripting"));
        rootStatement.Statements.Add(new UsingDeclaration("CasaEngine.Framework.World"));

        var namespaceName = "generatedScope";
        rootStatement.Statements.Add(new NamespaceDeclaration(namespaceName));
        var className = "GeneratedClass";
        var classDeclaration = new ClassDeclaration(className, nameof(GameplayProxy));
        rootStatement.Statements.Add(classDeclaration);
        classDeclaration.Body.Statements.AddRange(methodsStatement);

        _codeGenerator.GenerateCode(rootStatement);

        return new GeneratedClassInformations
        {
            Namespace = namespaceName,
            ClassName = className,
            Code = _stream.ToString(),
        };
    }

    public string GenerateClassCode2(IEnumerable<Statement> methodsStatement)
    {
        var rootStatement = new Block();
        rootStatement.Statements.AddRange(methodsStatement);

        _codeGenerator.GenerateCode(rootStatement);

        return _stream.ToString();
    }
}