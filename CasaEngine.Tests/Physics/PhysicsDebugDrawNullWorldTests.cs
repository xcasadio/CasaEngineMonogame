using CasaEngine.Framework.Application.Components.Physics;
using Xunit;

namespace CasaEngine.Tests.Physics;

/// <summary>
/// A view can reference a world with no physics context for a frame (world swap, play
/// session stop, world not loaded yet). Debug rendering must skip it, never fault:
/// it is an overlay, and faulting there takes the whole editor down.
/// </summary>
public class PhysicsDebugDrawNullWorldTests
{
    [Fact]
    public void DrawDebugWorld_NullPhysicsWorld_DoesNotThrow()
    {
        var drawer = new PhysicsDebugDrawComponent(null);

        drawer.DrawDebugWorld(null);
    }
}
