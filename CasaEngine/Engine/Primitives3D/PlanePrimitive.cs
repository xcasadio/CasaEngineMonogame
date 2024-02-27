using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Primitives3D;

public class PlanePrimitive : GeometricPrimitive
{
#if EDITOR
    private Vector2 _scale;
    private int _tessellationHorizontal, _tessellationVertical;
#endif

    public PlanePrimitive(float sizeH = 1.0f, float sizeV = 1.0f, int tessellationHorizontal = 1, int tessellationVertical = 1)
    {
        if (tessellationHorizontal < 1)
        {
            throw new ArgumentOutOfRangeException("PlanePrimitive() : tessellationHorizontal_");
        }

        if (tessellationVertical < 1)
        {
            throw new ArgumentOutOfRangeException("PlanePrimitive() : tessellationVertical_");
        }

#if EDITOR
        _scale = new Vector2(sizeH, sizeV);
        _tessellationHorizontal = tessellationHorizontal;
        _tessellationVertical = tessellationVertical;
#endif

        var uv = Vector2.Zero;

        uint verticalSegments = (uint)tessellationVertical + 1;
        uint horizontalSegments = (uint)tessellationHorizontal + 1;

        var sizeHBy2 = sizeH / 2.0f;
        var sizeVBy2 = sizeV / 2.0f;
        var stepH = sizeH / tessellationHorizontal;
        var stepV = sizeV / tessellationVertical;

        //increment to compute uv
        int stepHTotal = 0, stepVTotal;

        //Compute Vertex
        for (var dx = -sizeHBy2; dx <= sizeHBy2; dx += stepH)
        {
            stepVTotal = 0;

            for (var dz = -sizeVBy2; dz <= sizeVBy2; dz += stepV)
            {
                uv.X = (float)stepHTotal / tessellationHorizontal;
                uv.Y = (float)stepVTotal / tessellationVertical;
                AddVertex(new Vector3(dx, 0.0f, dz), Vector3.UnitY, uv);
                stepVTotal++;
            }

            stepHTotal++;
        }

        //Compute Index : compute quad
        for (uint iy = 0; iy < tessellationVertical; iy++)
        {
            for (uint ix = 0; ix < tessellationHorizontal; ix++)
            {
                //first triangle
                AddIndex(ix);
                AddIndex((iy + 1) * horizontalSegments + ix);
                AddIndex(ix + 1);

                //second triangle
                AddIndex(ix + 1);
                AddIndex((iy + 1) * horizontalSegments + ix);
                AddIndex((iy + 1) * horizontalSegments + ix + 1);
            }
        }
    }
}