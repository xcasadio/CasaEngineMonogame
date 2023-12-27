using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public class RenderGroup : IRenderGroup
{
    public bool HasDrawableElements()
    {
        return RenderGroupStateCache.Count > 0;
    }

    private Dictionary<Tuple<IPipelineState, PrimitiveType, VertexDeclaration>, List<IRenderGroupState>> RenderGroupStateCache;

    public static IRenderGroup Create()
    {
        return new RenderGroup();
    }

    protected RenderGroup()
    {
        RenderGroupStateCache = new Dictionary<Tuple<IPipelineState, PrimitiveType, VertexDeclaration>, List<IRenderGroupState>>();
    }

    public void Reset()
    {
        // TODO - maybe implement an LRU cache here so that the size of the cache doesn't
        // Grow indefinitely as a user navigates a scene.
        foreach (var rgs in GetStateList())
        {
            rgs.Elements.Clear();
        }

        // This call is *crazy* expensive
        //RenderGroupStateCache.Clear();
    }

    public IEnumerable<IRenderGroupState> GetStateList()
    {
        foreach (var renderGroupStateList in RenderGroupStateCache.Values)
        {
            foreach (var renderGroupState in renderGroupStateList)
            {
                yield return renderGroupState;
            }
        }
    }

    public IRenderGroupState GetOrCreateState(GraphicsDevice device, IPipelineState pso, PrimitiveType pt, VertexDeclaration vl)
    {
        var modelOffset = 64u;
        /*if (device.UniformBufferMinOffsetAlignment > 64)
        {
            modelOffset = device.UniformBufferMinOffsetAlignment;
        }*/

        var maxAllowedDrawables = 65536u / modelOffset;

        var key = new Tuple<IPipelineState, PrimitiveType, VertexDeclaration>(pso, pt, vl);
        if (RenderGroupStateCache.TryGetValue(key, out var renderGroupStateList))
        {
            // Check to see if this state list can accept any more drawables, if not, allocate a new one
            foreach (var renderGroupState in renderGroupStateList)
            {
                if (renderGroupState.Elements.Count < maxAllowedDrawables)
                {
                    return renderGroupState;
                }
            }

            renderGroupStateList.Add(RenderGroupState.Create(pso, pt, vl));
            return renderGroupStateList.Last();
        }

        renderGroupStateList = new List<IRenderGroupState> { RenderGroupState.Create(pso, pt, vl) };
        RenderGroupStateCache.Add(key, renderGroupStateList);

        return renderGroupStateList.Last();
    }
}