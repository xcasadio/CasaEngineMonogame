using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Veldrid.SceneGraph;

/// <summary>
/// Class representing convex clipping volumes made up from planes.
/// When adding planes, normals should point inward
/// </summary>
public class Polytope
{
    private readonly List<Plane> _planeList = new List<Plane>();

    private Matrix _viewProjectionMatrix = Matrix.Identity;
    public Matrix ViewProjectionMatrix => _viewProjectionMatrix;

    public Polytope()
    {
        SetToUnitFrustum();
    }

    public void SetViewProjectionMatrix(Matrix viewProjectionMatrix)
    {
        _viewProjectionMatrix = viewProjectionMatrix;
    }

    public void SetToUnitFrustum(bool withNear = true, bool withFar = true)
    {
        _planeList.Clear();
        _planeList.Add(Plane.Create(1.0f, 0.0f, 0.0f, 1.0f)); // left plane.
        _planeList.Add(Plane.Create(-1.0f, 0.0f, 0.0f, 1.0f)); // right plane.
        _planeList.Add(Plane.Create(0.0f, 1.0f, 0.0f, 1.0f)); // bottom plane.
        _planeList.Add(Plane.Create(0.0f, -1.0f, 0.0f, 1.0f)); // top plane.
        if (withNear)
        {
            _planeList.Add(Plane.Create(0.0f, 0.0f, 1.0f, 1.0f)); // near plane
        }

        if (withFar)
        {
            _planeList.Add(Plane.Create(0.0f, 0.0f, -1.0f, 1.0f)); // far plane
        }
    }

    public void SetToViewProjectionFrustum(
        Matrix viewProjectionMatrix,
        bool withNear = true,
        bool withFar = true)
    {
        SetToUnitFrustum(withNear, withFar);
        foreach (var plane in _planeList)
        {
            plane.Transform(viewProjectionMatrix);
        }
    }

    public bool Contains(BoundingBox bb)
    {
        if (_planeList.Count == 0) return true;

        foreach (var plane in _planeList)
        {
            var res = plane.Intersect(bb);
            if (res < 0) return false;  // Outside the clipping set
        }

        return true;
    }

    public bool Contains(BoundingBox bb, Matrix transformMatrix)
    {
        if (_planeList.Count == 0) return true;

        var mvp = Matrix.Multiply(transformMatrix, ViewProjectionMatrix);
        Matrix.Invert(ref mvp, out var mvpInv);

        foreach (var plane in _planeList)
        {
            var isp = Plane.Create(plane);
            isp.Transform(mvpInv);
            var res = isp.Intersect(bb);
            if (res < 0) return false;  // Outside the clipping set
        }

        return true;
    }
}