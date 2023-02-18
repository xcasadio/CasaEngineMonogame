//-----------------------------------------------------------------------------
// VertexPositionNormal.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------



namespace CasaEngine.Engine.Primitives3D
{
    /// <summary>
    /// Custom vertex type for vertices that have just a
    /// position and a normal, without any texture coordinates.
    /// </summary>
    /*public struct VertexPositionNormal
    {
        public Vector3 Position;
        public Vector3 Normal;

        /// <summary>
        /// Constructor.
        /// </summary>
        public VertexPositionNormal(Vector3 position, Vector3 normal)
        {
            Position = position;
            Normal = normal;
        }

        /// <summary>
        /// Vertex format information, used to create a VertexDeclaration.
        /// </summary>
        public static readonly VertexElement[] VertexElements =
        {
            new VertexElement(0, 0, VertexElementFormat.Vector3,
                                    VertexElementMethod.Default,
                                    VertexElementUsage.Position, 0),

            new VertexElement(0, 12, VertexElementFormat.Vector3,
                                     VertexElementMethod.Default,
                                     VertexElementUsage.Normal, 0),
        };

        /// <summary>
        /// Size of this vertex type.
        /// </summary>
        public const int SizeInBytes = 24;
    }*/
}
