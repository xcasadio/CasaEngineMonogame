using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Core.Helpers;

public static class BoundingBoxHelper
{
    public static BoundingBox Create()
    {
        var boundingBox = new BoundingBox();
        boundingBox.Init();
        return boundingBox;
    }

    public static void Init(this BoundingBox boundingBox)
    {
        boundingBox.Min.X = float.PositiveInfinity;
        boundingBox.Min.Y = float.PositiveInfinity;
        boundingBox.Min.Z = float.PositiveInfinity;

        boundingBox.Max.X = float.NegativeInfinity;
        boundingBox.Max.Y = float.NegativeInfinity;
        boundingBox.Max.Z = float.NegativeInfinity;
    }

    public static bool Valid(this BoundingBox boundingBox)
    {
        return boundingBox.Max.X >= boundingBox.Min.X &&
               boundingBox.Max.Y >= boundingBox.Min.Y &&
               boundingBox.Max.Z >= boundingBox.Min.Z;
    }

    public static void ExpandBy(this BoundingBox boundingBox, BoundingBox bb)
    {
        if (!bb.Valid()) return;

        if (bb.Min.X < boundingBox.Min.X) boundingBox.Min.X = bb.Min.X;
        if (bb.Max.X > boundingBox.Max.X) boundingBox.Max.X = bb.Max.X;

        if (bb.Min.Y < boundingBox.Min.Y) boundingBox.Min.Y = bb.Min.Y;
        if (bb.Max.Y > boundingBox.Max.Y) boundingBox.Max.Y = bb.Max.Y;

        if (bb.Min.Z < boundingBox.Min.Z) boundingBox.Min.Z = bb.Min.Z;
        if (bb.Max.Z > boundingBox.Max.Z) boundingBox.Max.Z = bb.Max.Z;
    }

    public static void ExpandBy(this BoundingBox boundingBox, BoundingSphere sh)
    {
        if (!sh.Valid()) return;

        if (sh.Center.X - sh.Radius < boundingBox.Min.X) boundingBox.Min.X = sh.Center.X - sh.Radius;
        if (sh.Center.X + sh.Radius > boundingBox.Max.X) boundingBox.Max.X = sh.Center.X + sh.Radius;

        if (sh.Center.Y - sh.Radius < boundingBox.Min.Y) boundingBox.Min.Y = sh.Center.Y - sh.Radius;
        if (sh.Center.Y + sh.Radius > boundingBox.Max.Y) boundingBox.Max.Y = sh.Center.Y + sh.Radius;

        if (sh.Center.Z - sh.Radius < boundingBox.Min.Z) boundingBox.Min.Z = sh.Center.Z - sh.Radius;
        if (sh.Center.Z + sh.Radius > boundingBox.Max.Z) boundingBox.Max.Z = sh.Center.Z + sh.Radius;
    }

    public static Vector3 GetCenter(this BoundingBox boundingBox) => (boundingBox.Min + boundingBox.Max) * 0.5f;

    public static float GetRadius(this BoundingBox boundingBox)
    {
        return (float)Math.Sqrt(boundingBox.GetRadiusSquared());
    }

    public static float GetRadiusSquared(this BoundingBox boundingBox)
    {
        return 0.25f * ((boundingBox.Max - boundingBox.Min).LengthSquared());
    }

    /// <summary>
    /// Returns a specific corner of the bounding box.
    /// pos specifies the corner as a number between 0 and 7.
    /// Each bit selects an axis, X, Y, or Z from least- to
    /// most-significant. Unset bits select the minimum value
    /// for that axis, and set bits select the maximum.
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public static Vector3 Corner(this BoundingBox boundingBox, uint pos)
    {
        return new Vector3(
            (pos & 1) > 0 ? boundingBox.Max.X : boundingBox.Min.X,
            (pos & 2) > 0 ? boundingBox.Max.Y : boundingBox.Min.Y,
            (pos & 4) > 0 ? boundingBox.Max.Z : boundingBox.Min.Z);
    }

    public static BoundingBox CalculateBoundingBoxFromModel(Model model)
    {
        // NOTE: we could use ModelMesh's built in BoundingSphere property 
        // to create a bounding box with BoundingBox.CreateFromSphere,
        // but the source spheres are already overestimates and 
        // box from sphere is an additional overestimate, resulting
        // in an unnecessarily huge bounding box

        Vector3 min = Vector3.One * float.MaxValue;
        Vector3 max = Vector3.One * float.MinValue;

        Matrix[] boneTransforms = new Matrix[model.Bones.Count];
        model.CopyAbsoluteBoneTransformsTo(boneTransforms);

        foreach (ModelMesh mesh in model.Meshes)
        {
            foreach (ModelMeshPart part in mesh.MeshParts)
            {
                byte[] data = new byte[part.VertexBuffer.VertexCount];
                part.VertexBuffer.GetData(data);

                VertexDeclaration decl = part.VertexBuffer.VertexDeclaration;
                VertexElement[] elem = decl.GetVertexElements();

                int stride = decl.VertexStride;

                int posAt = -1;
                for (int i = 0; i < elem.Length; ++i)
                {
                    if (elem[i].VertexElementUsage != VertexElementUsage.Position)
                    {
                        continue;
                    }

                    posAt = elem[i].Offset;
                    break;
                }

                if (posAt == -1)
                {
                    throw new Exception("No position data?!?!");
                }

                for (int i = 0; i < data.Length; i += stride)
                {
                    int ind = i + posAt;
                    int fs = sizeof(float);

                    float x = BitConverter.ToSingle(data, ind);
                    float y = BitConverter.ToSingle(data, ind + fs);
                    float z = BitConverter.ToSingle(data, ind + fs * 2);

                    Vector3 vec3 = new Vector3(x, y, z);
                    Matrix transform = boneTransforms[mesh.ParentBone.Index];
                    vec3 = Vector3.Transform(vec3, transform);

                    if (vec3.X < min.X)
                    {
                        min.X = vec3.X;
                    }

                    if (vec3.X > max.X)
                    {
                        max.X = vec3.X;
                    }

                    if (vec3.Y < min.Y)
                    {
                        min.Y = vec3.Y;
                    }

                    if (vec3.Y > max.Y)
                    {
                        max.Y = vec3.Y;
                    }

                    if (vec3.Z < min.Z)
                    {
                        min.Z = vec3.Z;
                    }

                    if (vec3.Z > max.Z)
                    {
                        max.Z = vec3.Z;
                    }
                }
            }
        }

        return new BoundingBox(min, max);
    }
}