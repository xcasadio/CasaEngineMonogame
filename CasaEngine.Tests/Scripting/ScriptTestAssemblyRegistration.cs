using System.Runtime.CompilerServices;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Tests.Scripting;

/// <summary>
/// Script objects are created by full name from the registered gameplay assemblies only (ADR-0075). The test
/// assembly plays the role of the gameplay assembly: the stub proxies, controllers and modes declared in the tests
/// are resolved through it, so it is registered once for the whole test process.
/// </summary>
internal static class ScriptTestAssemblyRegistration
{
    internal static System.Reflection.Assembly TestAssembly => typeof(ScriptTestAssemblyRegistration).Assembly;

    [ModuleInitializer]
    internal static void Register() => ElementFactory.RegisterScriptAssembly(TestAssembly);

    /// <summary>
    /// Runs <paramref name="action"/> with the test assembly unregistered, then registers it again. Callers
    /// belong to <see cref="ProjectEnvironmentCollection"/>, which never runs next to another test.
    /// </summary>
    internal static void WithTestAssemblyUnregistered(Action action)
    {
        ElementFactory.UnregisterScriptAssembly(TestAssembly);
        try
        {
            action();
        }
        finally
        {
            ElementFactory.RegisterScriptAssembly(TestAssembly);
        }
    }
}
