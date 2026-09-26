namespace CasaEngine.Tests;

/// <summary>
/// Opens a window measured with <see cref="GC.GetAllocatedBytesForCurrentThread"/> in a zero-allocation test:
/// <c>var before = AllocationWindow.Start();</c>, run the code under test, then assert that
/// <c>GC.GetAllocatedBytesForCurrentThread() - before</c> is 0.<para/>
/// Why not read the counter directly: it reports the bytes handed to this thread minus the unused tail of the thread's
/// current allocation context, and a background GC voids every thread's allocation context at the end of its mark
/// phase without taking that tail back (.NET 9 gc.cpp, <c>repair_allocation_contexts(FALSE)</c> -> <c>void_allocation</c>).
/// Under the full suite's parallel load, where other test threads keep triggering background GCs, such a GC could land
/// in the window and add up to ~8 KB that were never allocated. A blocking GC empties the context with exact
/// accounting, and an empty context has nothing left to void, so from the returned value on the counter only moves if
/// this thread allocates.
/// </summary>
internal static class AllocationWindow
{
    /// <summary>
    /// Empties this thread's allocation context with a blocking gen0 collection, then returns
    /// <see cref="GC.GetAllocatedBytesForCurrentThread"/>. Nothing may allocate between this call and the code under
    /// test.
    /// </summary>
    public static long Start()
    {
        GC.Collect(0, GCCollectionMode.Forced, blocking: true);
        return GC.GetAllocatedBytesForCurrentThread();
    }
}
