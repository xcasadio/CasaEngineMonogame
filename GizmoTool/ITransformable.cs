// -------------------------------------------------------------
// -- XNA 3D Gizmo (Component)
// -------------------------------------------------------------
// -- open-source gizmo component for any 3D level editor.
// -- contains any feature you may be looking for in a transformation gizmo.
// -- 
// -- for additional information and instructions visit codeplex.
// --
// -- codeplex url: http://xnagizmo.codeplex.com/
// --
// -----------------Please Do Not Remove ----------------------
// -- Work by Tom Looman, licensed under Ms-PL
// -- My Blog: http://coreenginedev.blogspot.com
// -- My Portfolio: http://tomlooman.com
// -- You may find additional XNA resources and information on these sites.
// ------------------------------------------------------------
// -- uncomment the define statements below to choose your desired rotation method.

#define USE_QUATERNION
//#define USE_ROTATIONMATRIX
//#define USE_NAME

using Microsoft.Xna.Framework;

namespace XNAGizmo
{
    public interface ITransformable
    {
#if USE_NAME
        string Name { get; }
#endif

        Vector3 Position { get; set; }
        Vector3 Scale { get; set; }

#if USE_QUATERNION

        Quaternion Orientation { get; set; }
        Vector3 Forward { get; }
        Vector3 Up { get; }

#elif USE_ROTATIONMATRIX
        Matrix Rotation { get; set; }
#else
        Vector3 Forward { get; set; }
        Vector3 Up { get; set; }
#endif

        BoundingBox BoundingBox { get; }
    }
}
