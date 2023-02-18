//-----------------------------------------------------------------------------
// CubePrimitive.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------



namespace CasaEngine.Engine.Primitives3D
{
    /// <summary>
    /// Geometric primitive class for drawing cubes.
    /// </summary>
    public partial class BoxPrimitive : Geometric3DPrimitive
    {

        float m_Width, m_Height, m_Length;



#if BINARY_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binW_"></param>
		/// <param name="linearize_"></param>
		public override void Save(System.IO.BinaryWriter binW_, bool linearize_)
		{
			base.Save(binW_, linearize_);

			binW_.Write(m_Width);
			binW_.Write(m_Height);
			binW_.Write(m_Length);

		}

#elif XML_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public override void Save(XmlElement el_, SaveOption option_)
		{
			base.Save(el_, option_);

			XmlElement meshNode = (XmlElement)el_.SelectSingleNode("Mesh");

			el_.OwnerDocument.AddAttribute(meshNode, "width", m_Width.ToString());
			el_.OwnerDocument.AddAttribute(meshNode, "height", m_Height.ToString());
			el_.OwnerDocument.AddAttribute(meshNode, "length", m_Length.ToString());
		}

#endif

    }
}
