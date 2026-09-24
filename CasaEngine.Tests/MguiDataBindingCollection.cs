using Xunit;

namespace CasaEngine.Tests;

/// <summary>
/// Tests that create MGUI data bindings (loading XAML with <c>{dataBinding:MGBinding …}</c>, or setting a window's
/// data context). MGUI keeps every binding in a static, single-threaded registry
/// (<c>MGUI/MGUI.Core/UI/DataBinding/DataBindingManager.cs</c>, <c>_Bindings</c> and <c>_BindingsByTargetObject</c>):
/// the UI runs on one thread, but xUnit runs test classes in parallel, and two classes adding bindings at once
/// corrupt its dictionary ("Operations that change non-concurrent collections must have exclusive access").
/// Every test class that creates bindings joins this collection, which runs alone.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MguiDataBindingCollection
{
    public const string Name = "MguiDataBinding";
}
