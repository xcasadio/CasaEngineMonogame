using CasaEngine.Core.Packing;
using Xunit;

namespace CasaEngine.Tests.Packing;

public sealed class OutOfSpaceExceptionTests
{
    [Fact]
    public void DefaultConstructorProducesReadableException()
    {
        OutOfSpaceException exception = new();
        Assert.False(string.IsNullOrWhiteSpace(exception.ToString()));
    }

    [Fact]
    public void MessageConstructorPreservesMessage()
    {
        OutOfSpaceException exception = new("Hello World");
        Assert.Equal("Hello World", exception.Message);
    }

    [Fact]
    public void InnerExceptionConstructorPreservesInnerException()
    {
        Exception inner = new("This is a test");
        OutOfSpaceException exception = new("Hello World", inner);
        Assert.Same(inner, exception.InnerException);
    }
}