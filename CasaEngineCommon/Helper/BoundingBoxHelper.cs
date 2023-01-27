using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace CasaEngineCommon.Helper
{
	public static class BoundingBoxHelper
	{
		/// <summary>
		/// Calculates the bounding box that contains the sum of the bounding boxes
		/// of the model node's mesh parts (Editor only)
		/// </summary>
		public static BoundingBox CalculateBoundingBoxFromModel(Model model_)
		{
            throw new NotImplementedException();
            /*
			Model model = model_;

			// NOTE: we could use ModelMesh's built in BoundingSphere property 
			// to create a bounding box with BoundingBox.CreateFromSphere,
			// but the source spheres are already overestimates and 
			// box from sphere is an additional overestimate, resulting
			// in an unnecessarily huge bounding box

			// assume the worst case ;)
			Vector3 min = Vector3.One * float.MaxValue;
			Vector3 max = Vector3.One * float.MinValue;

			Matrix[] boneTransforms = new Matrix[model.Bones.Count];
			model.CopyAbsoluteBoneTransformsTo(boneTransforms);

			foreach (ModelMesh mesh in model.Meshes)
			{
				// grab the raw vertex data from the mesh's vertex buffer
				byte[] data = new byte[mesh.VertexBuffer.SizeInBytes];
				mesh.VertexBuffer.GetData(data);

				// iterate over each part, comparing all vertex positions with
				// the current min and max, updating as necessary
				foreach (ModelMeshPart part in mesh.MeshParts)
				{
					VertexDeclaration decl = part.VertexDeclaration;
					VertexElement[] elem = decl.GetVertexElements();

					int stride = decl.GetVertexStrideSize(0);

					// find the vertex stream offset of the vertex data's position element
					short pos_at = -1;
					for (int i = 0; i < elem.Length; ++i)
					{
						// not interested...
						if (elem[i].VertexElementUsage != VertexElementUsage.Position)
							continue;

						// save the offset
						pos_at = elem[i].Offset;
						break;
					}

					// didn't find the position element... not good
					if (pos_at == -1)
						throw new Exception("No position data?!?!");

					// decode the position of each vertex in the stream and
					// compare its value to the min/max of the bounding box
					for (int i = 0; i < data.Length; i += stride)
					{
						int ind = i + pos_at;
						int fs = sizeof(float);

						float x = BitConverter.ToSingle(data, ind);
						float y = BitConverter.ToSingle(data, ind + fs);
						float z = BitConverter.ToSingle(data, ind + (fs * 2));

						Vector3 vec3 = new Vector3(x, y, z);
						Matrix transform = boneTransforms[mesh.ParentBone.Index];
						vec3 = Vector3.Transform(vec3, transform);

						// if position is outside bounding box, then update the
						// bounding box min/max to fit it
						if (vec3.X < min.X) min.X = vec3.X;
						if (vec3.X > max.X) max.X = vec3.X;
						if (vec3.Y < min.Y) min.Y = vec3.Y;
						if (vec3.Y > max.Y) max.Y = vec3.Y;
						if (vec3.Z < min.Z) min.Z = vec3.Z;
						if (vec3.Z > max.Z) max.Z = vec3.Z;
					}
				}
			}

			return new BoundingBox(min, max);*/
		}
	}
}
