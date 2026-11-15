using System.Runtime.CompilerServices;
using CasaEngine.Framework.Input;

namespace CasaEngine.Tests.Input;

/// <summary>
/// Keyboard bindings are read by key position on the active layout (ADR-0096). The tests read them on a US QWERTY layout,
/// whatever the layout of the machine running them, so a test that presses W through a mapping gets W everywhere.
/// <see cref="KeyboardLayoutTests"/>, in a collection that never runs in parallel, sets other layouts and puts this one
/// back.
/// </summary>
internal static class TestKeyboardLayout
{
    [ModuleInitializer]
    internal static void UseQwerty()
    {
        KeyboardLayout.UseLayout(KeyboardLayoutCollection.Qwerty);
    }
}
