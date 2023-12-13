using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Core.Helpers;

public static class BoundingBoxHelper
{
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