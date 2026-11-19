using System.ComponentModel;
using System.Runtime.CompilerServices;
using CasaEngine.Editor.Controls;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests.Scripting;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>Not offered by the Add Component dialog.</summary>
[Browsable(false)]
public class AddComponentListHiddenComponent : EntityComponent
{
    public override EntityComponent Clone() => new AddComponentListHiddenComponent();
}

/// <summary>Offered by the Add Component dialog.</summary>
public class AddComponentListVisibleComponent : EntityComponent
{
    public override EntityComponent Clone() => new AddComponentListVisibleComponent();
}

/// <summary>The Add Component list follows the assemblies <see cref="ElementFactory"/> resolves names in, at every call.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AddComponentListTests
{
    [Fact]
    public void List_OffersAddableComponents_AndHidesTheOthers()
    {
        var types = EntityDetailsPanel.BuildAddableComponentTypes();

        Assert.Contains(nameof(AddComponentListVisibleComponent), types.Keys);
        Assert.DoesNotContain(nameof(AddComponentListHiddenComponent), types.Keys);
        Assert.DoesNotContain(nameof(UnresolvedComponent), types.Keys);
        Assert.Contains("Light", types.Keys);
    }

    [Fact]
    public void List_FollowsTheRegisteredScriptAssembly()
    {
        string directory = Path.Combine(Path.GetTempPath(), "casa-add-component-list-" + Guid.NewGuid().ToString("N"));
        var host = new ScriptAssemblyHost();
        try
        {
            string assemblyPath = ScriptAssemblyCompiler.CompileScriptAssembly(directory, "AddComponentListScripts", version: 1,
                ElementFactoryReloadTests.AnnotatedComponentSource);

            Assert.DoesNotContain("TestScriptAnnotatedComponent", EntityDetailsPanel.BuildAddableComponentTypes().Keys);

            RegisterAndAssertListed(host, assemblyPath);

            ElementFactory.UnregisterScriptAssembly(host.LoadedAssembly!);

            //  A cached list would still offer the component of the unregistered assembly.
            Assert.DoesNotContain("TestScriptAnnotatedComponent", EntityDetailsPanel.BuildAddableComponentTypes().Keys);
        }
        finally
        {
            if (host.LoadedAssembly != null)
            {
                ElementFactory.UnregisterScriptAssembly(host.LoadedAssembly);
            }

            host.Unload();
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterAndAssertListed(ScriptAssemblyHost host, string assemblyPath)
    {
        ElementFactory.RegisterScriptAssembly(host.Load(assemblyPath));

        Assert.Contains("TestScriptAnnotatedComponent", EntityDetailsPanel.BuildAddableComponentTypes().Keys);
    }
}
