//-----------------------------------------------------------------------------
// SpherePrimitive.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------



namespace CasaEngine.Engine.Primitives3D
{
    /// <summary>
    /// Geometric primitive class for drawing spheres.
    /// </summary>
    public partial class SpherePrimitive
        : Geometric3DPrimitive
    {

        float m_Diameter;
        int m_Tessellation;



#if BINARY_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binW_"></param>
		/// <param name="linearize_"></param>
		public override void Save(System.IO.BinaryWriter binW_, bool linearize_)
		{
			base.Save(binW_, linearize_);

			binW_.Write(m_Tessellation);
			binW_.Write(m_Diameter);
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

			el_.OwnerDocument.AddAttribute(meshNode, "tessellation", m_Tessellation.ToString());
			el_.OwnerDocument.AddAttribute(meshNode, "diameter", m_Diameter.ToString());
		}

#endif

    }
}
