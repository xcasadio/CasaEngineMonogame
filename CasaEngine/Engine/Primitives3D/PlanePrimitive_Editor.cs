

using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Primitives3D
{
    public partial class PlanePrimitive
        : Geometric3DPrimitive
    {

        Vector2 m_Scale;
        int m_TessellationHorizontal, m_TessellationVertical;



#if BINARY_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binW_"></param>
		/// <param name="linearize_"></param>
		public override void Save(System.IO.BinaryWriter binW_, bool linearize_)
		{
			base.Save(binW_, linearize_);

			binW_.Write(m_TessellationHorizontal);
			binW_.Write(m_TessellationVertical);
			binW_.Write(m_Scale);
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

			el_.OwnerDocument.AddAttribute(meshNode, "TessellationH", m_TessellationHorizontal.ToString());
			el_.OwnerDocument.AddAttribute(meshNode, "TessellationV", m_TessellationVertical.ToString());
			XmlElement el = el_.OwnerDocument.CreateElement("Scale", m_Scale);
			meshNode.AppendChild(el);
		}

#endif

    }
}
