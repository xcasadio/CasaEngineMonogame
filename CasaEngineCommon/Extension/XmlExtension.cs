using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.Globalization;

namespace CasaEngineCommon.Extension
{
	/// <summary>
	/// 
	/// </summary>
	static public class XMLExtension
	{
        







		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlEl_"></param>
		/// <param name="c_"></param>
		static public void Read(this XmlElement xmlEl_, ref Color c_)
		{
            c_.R = byte.Parse(xmlEl_.Attributes["r"].Value, CultureInfo.InvariantCulture.NumberFormat);
            c_.G = byte.Parse(xmlEl_.Attributes["g"].Value, CultureInfo.InvariantCulture.NumberFormat);
            c_.B = byte.Parse(xmlEl_.Attributes["b"].Value, CultureInfo.InvariantCulture.NumberFormat);
            c_.A = byte.Parse(xmlEl_.Attributes["a"].Value, CultureInfo.InvariantCulture.NumberFormat);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlEl_"></param>
		/// <param name="v_"></param>
		static public void Read(this XmlElement xmlEl_, ref Vector4 v_)
		{
            v_.X = float.Parse(xmlEl_.Attributes["x"].Value, CultureInfo.InvariantCulture.NumberFormat);
            v_.Y = float.Parse(xmlEl_.Attributes["y"].Value, CultureInfo.InvariantCulture.NumberFormat);
            v_.Z = float.Parse(xmlEl_.Attributes["z"].Value, CultureInfo.InvariantCulture.NumberFormat);
            v_.W = float.Parse(xmlEl_.Attributes["w"].Value, CultureInfo.InvariantCulture.NumberFormat);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlEl_"></param>
		/// <param name="v_"></param>
		static public void Read(this XmlElement xmlEl_, ref BoundingBox b_)
		{
			((XmlElement)xmlEl_.SelectSingleNode("Min")).Read(ref b_.Min);
			((XmlElement)xmlEl_.SelectSingleNode("Max")).Read(ref b_.Max);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlEl_"></param>
		/// <param name="v_"></param>
		static public void Read(this XmlElement xmlEl_, ref Vector3 v_)
		{
            v_.X = float.Parse(xmlEl_.Attributes["x"].Value, CultureInfo.InvariantCulture.NumberFormat);
            v_.Y = float.Parse(xmlEl_.Attributes["y"].Value, CultureInfo.InvariantCulture.NumberFormat);
            v_.Z = float.Parse(xmlEl_.Attributes["z"].Value, CultureInfo.InvariantCulture.NumberFormat);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlEl_"></param>
		/// <param name="v_"></param>
		static public void Read(this XmlElement xmlEl_, ref Vector2 v_)
		{
            v_.X = float.Parse(xmlEl_.Attributes["x"].Value, CultureInfo.InvariantCulture.NumberFormat);
            v_.Y = float.Parse(xmlEl_.Attributes["y"].Value, CultureInfo.InvariantCulture.NumberFormat);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlEl_"></param>
		/// <param name="v_"></param>
		static public void Read(this XmlElement xmlEl_, ref Matrix m_)
		{
            m_.M11 = float.Parse(xmlEl_.Attributes["m11"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M12 = float.Parse(xmlEl_.Attributes["m12"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M13 = float.Parse(xmlEl_.Attributes["m13"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M14 = float.Parse(xmlEl_.Attributes["m14"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M21 = float.Parse(xmlEl_.Attributes["m21"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M22 = float.Parse(xmlEl_.Attributes["m22"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M23 = float.Parse(xmlEl_.Attributes["m23"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M24 = float.Parse(xmlEl_.Attributes["m24"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M31 = float.Parse(xmlEl_.Attributes["m31"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M32 = float.Parse(xmlEl_.Attributes["m32"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M33 = float.Parse(xmlEl_.Attributes["m33"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M34 = float.Parse(xmlEl_.Attributes["m34"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M41 = float.Parse(xmlEl_.Attributes["m41"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M42 = float.Parse(xmlEl_.Attributes["m42"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M43 = float.Parse(xmlEl_.Attributes["m43"].Value, CultureInfo.InvariantCulture.NumberFormat);
            m_.M44 = float.Parse(xmlEl_.Attributes["m44"].Value, CultureInfo.InvariantCulture.NumberFormat);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlEl_"></param>
		/// <param name="v_"></param>
		static public void Read(this XmlElement xmlEl_, ref Quaternion q_)
		{
            q_.X = float.Parse(xmlEl_.Attributes["x"].Value, CultureInfo.InvariantCulture.NumberFormat);
            q_.Y = float.Parse(xmlEl_.Attributes["y"].Value, CultureInfo.InvariantCulture.NumberFormat);
            q_.Z = float.Parse(xmlEl_.Attributes["z"].Value, CultureInfo.InvariantCulture.NumberFormat);
            q_.W = float.Parse(xmlEl_.Attributes["w"].Value, CultureInfo.InvariantCulture.NumberFormat);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlEl_"></param>
		/// <param name="v_"></param>
		static public void Read(this XmlElement xmlEl_, ref Rectangle r_)
		{
            r_.X = int.Parse(xmlEl_.Attributes["x"].Value, CultureInfo.InvariantCulture.NumberFormat);
            r_.Y = int.Parse(xmlEl_.Attributes["y"].Value, CultureInfo.InvariantCulture.NumberFormat);
            r_.Width = int.Parse(xmlEl_.Attributes["width"].Value, CultureInfo.InvariantCulture.NumberFormat);
            r_.Height = int.Parse(xmlEl_.Attributes["height"].Value, CultureInfo.InvariantCulture.NumberFormat);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlEl_"></param>
		/// <param name="v_"></param>
		static public void Read(this XmlElement xmlEl_, ref Point p_)
		{
            p_.X = int.Parse(xmlEl_.Attributes["x"].Value, CultureInfo.InvariantCulture.NumberFormat);
            p_.Y = int.Parse(xmlEl_.Attributes["y"].Value, CultureInfo.InvariantCulture.NumberFormat);
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		static public XmlElement AddRootNode(this XmlDocument xmlDoc_, string nodeName_)
		{
			//let's add the XML declaration section
			XmlNode xmlnode = xmlDoc_.CreateNode(XmlNodeType.XmlDeclaration, "", "");
			xmlDoc_.AppendChild(xmlnode);

			//let's add the root element
			XmlElement xmlElem = xmlDoc_.CreateElement("", nodeName_, "");
			xmlDoc_.AppendChild(xmlElem);

			return xmlElem;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="xmlElement_"></param>
		/// <param name="attributeName_"></param>
		/// <param name="value_"></param>
		static public void AddAttribute(this XmlDocument xmlDoc_, XmlElement xmlElement_, string attributeName_, string value_)
		{
			XmlAttribute att = xmlDoc_.CreateAttribute(attributeName_);
			att.Value = value_;
			xmlElement_.Attributes.Append(att);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		/// <param name="val_"></param>
		/// <returns></returns>
		static public XmlElement CreateElementWithText(this XmlDocument xmlDoc_, string nodeName_, string val_)
		{
			XmlElement el = xmlDoc_.CreateElement(nodeName_);
			XmlText txtXML = xmlDoc_.CreateTextNode(val_);
			el.AppendChild(txtXML);

			return el;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		/// <param name="c_"></param>
		static public XmlElement CreateElement(this XmlDocument xmlDoc_, string nodeName_, Color c_)
		{
			XmlAttribute attR = xmlDoc_.CreateAttribute("r");
			attR.Value = c_.R.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute attG = xmlDoc_.CreateAttribute("g");
            attG.Value = c_.G.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute attB = xmlDoc_.CreateAttribute("b");
            attB.Value = c_.B.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute attA = xmlDoc_.CreateAttribute("a");
            attA.Value = c_.A.ToString(CultureInfo.InvariantCulture.NumberFormat);

			XmlElement el = xmlDoc_.CreateElement(nodeName_);
			el.Attributes.Append(attR);
			el.Attributes.Append(attG);
			el.Attributes.Append(attB);
			el.Attributes.Append(attA);

			return el;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		/// <param name="v_"></param>
		static public XmlElement CreateElement(this XmlDocument xmlDoc_, string nodeName_, Vector2 v_)
		{
			XmlAttribute att1 = xmlDoc_.CreateAttribute("x");
            att1.Value = v_.X.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att2 = xmlDoc_.CreateAttribute("y");
            att2.Value = v_.Y.ToString(CultureInfo.InvariantCulture.NumberFormat);

			XmlElement el = xmlDoc_.CreateElement(nodeName_);
			el.Attributes.Append(att1);
			el.Attributes.Append(att2);

			return el;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		/// <param name="v_"></param>
		static public XmlElement CreateElement(this XmlDocument xmlDoc_, string nodeName_, Vector3 v_)
		{
			XmlAttribute att1 = xmlDoc_.CreateAttribute("x");
            att1.Value = v_.X.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att2 = xmlDoc_.CreateAttribute("y");
            att2.Value = v_.Y.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att3 = xmlDoc_.CreateAttribute("z");
            att3.Value = v_.Z.ToString(CultureInfo.InvariantCulture.NumberFormat);

			XmlElement el = xmlDoc_.CreateElement(nodeName_);
			el.Attributes.Append(att1);
			el.Attributes.Append(att2);
			el.Attributes.Append(att3);

			return el;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		/// <param name="v_"></param>
		static public XmlElement CreateElement(this XmlDocument xmlDoc_, string nodeName_, Vector4 v_)
		{
			XmlAttribute att1 = xmlDoc_.CreateAttribute("x");
            att1.Value = v_.X.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att2 = xmlDoc_.CreateAttribute("y");
            att2.Value = v_.Y.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att3 = xmlDoc_.CreateAttribute("z");
            att3.Value = v_.Z.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att4 = xmlDoc_.CreateAttribute("w");
            att4.Value = v_.W.ToString(CultureInfo.InvariantCulture.NumberFormat);

			XmlElement el = xmlDoc_.CreateElement(nodeName_);
			el.Attributes.Append(att1);
			el.Attributes.Append(att2);
			el.Attributes.Append(att3);
			el.Attributes.Append(att4);

			return el;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		/// <param name="b_"></param>
		static public XmlElement CreateElement(this XmlDocument xmlDoc_, string nodeName_, BoundingBox b_)
		{
			XmlElement el = xmlDoc_.CreateElement(nodeName_);
			el.AppendChild(xmlDoc_.CreateElement("Min", b_.Min));
			el.AppendChild(xmlDoc_.CreateElement("Max", b_.Max));
			return el;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		/// <param name="q_"></param>
		static public XmlElement CreateElement(this XmlDocument xmlDoc_, string nodeName_, Quaternion q_)
		{
			Vector4 v = new Vector4(q_.X, q_.Y, q_.Z, q_.W);
			return CreateElement(xmlDoc_, nodeName_, v);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		/// <param name="m_"></param>
		static public XmlElement CreateElement(this XmlDocument xmlDoc_, string nodeName_, Matrix m_)
		{
			XmlAttribute att11 = xmlDoc_.CreateAttribute("m11");
			att11.Value = m_.M11.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att12 = xmlDoc_.CreateAttribute("m12");
			att12.Value = m_.M12.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att13 = xmlDoc_.CreateAttribute("m13");
			att13.Value = m_.M13.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att14 = xmlDoc_.CreateAttribute("m14");
			att14.Value = m_.M14.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att21 = xmlDoc_.CreateAttribute("m21");
			att21.Value = m_.M21.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att22 = xmlDoc_.CreateAttribute("m22");
			att22.Value = m_.M22.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att23 = xmlDoc_.CreateAttribute("m23");
			att23.Value = m_.M23.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att24 = xmlDoc_.CreateAttribute("m24");
			att24.Value = m_.M24.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att31 = xmlDoc_.CreateAttribute("m31");
			att31.Value = m_.M31.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att32 = xmlDoc_.CreateAttribute("m32");
			att32.Value = m_.M32.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att33 = xmlDoc_.CreateAttribute("m33");
			att33.Value = m_.M33.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att34 = xmlDoc_.CreateAttribute("m34");
			att34.Value = m_.M34.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att41 = xmlDoc_.CreateAttribute("m41");
			att41.Value = m_.M41.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att42 = xmlDoc_.CreateAttribute("m42");
			att42.Value = m_.M42.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att43 = xmlDoc_.CreateAttribute("m43");
			att43.Value = m_.M43.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att44 = xmlDoc_.CreateAttribute("m44");
			att44.Value = m_.M44.ToString(CultureInfo.InvariantCulture.NumberFormat);

			XmlElement el = xmlDoc_.CreateElement(nodeName_);
			el.Attributes.Append(att11);
			el.Attributes.Append(att12);
			el.Attributes.Append(att13);
			el.Attributes.Append(att14);
			el.Attributes.Append(att21);
			el.Attributes.Append(att22);
			el.Attributes.Append(att23);
			el.Attributes.Append(att24);
			el.Attributes.Append(att31);
			el.Attributes.Append(att32);
			el.Attributes.Append(att33);
			el.Attributes.Append(att34);
			el.Attributes.Append(att41);
			el.Attributes.Append(att42);
			el.Attributes.Append(att43);
			el.Attributes.Append(att44);

			return el;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		/// <param name="v_"></param>
		static public XmlElement CreateElement(this XmlDocument xmlDoc_, string nodeName_, Rectangle r_)
		{
			XmlAttribute att1 = xmlDoc_.CreateAttribute("x");
			att1.Value = r_.X.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att2 = xmlDoc_.CreateAttribute("y");
			att2.Value = r_.Y.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att3 = xmlDoc_.CreateAttribute("width");
			att3.Value = r_.Width.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att4 = xmlDoc_.CreateAttribute("height");
			att4.Value = r_.Height.ToString(CultureInfo.InvariantCulture.NumberFormat);

			XmlElement el = xmlDoc_.CreateElement(nodeName_);
			el.Attributes.Append(att1);
			el.Attributes.Append(att2);
			el.Attributes.Append(att3);
			el.Attributes.Append(att4);

			return el;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="xmlDoc_"></param>
		/// <param name="nodeName_"></param>
		/// <param name="v_"></param>
		static public XmlElement CreateElement(this XmlDocument xmlDoc_, string nodeName_, Point p_)
		{
			XmlAttribute att1 = xmlDoc_.CreateAttribute("x");
			att1.Value = p_.X.ToString(CultureInfo.InvariantCulture.NumberFormat);
			XmlAttribute att2 = xmlDoc_.CreateAttribute("y");
			att2.Value = p_.Y.ToString(CultureInfo.InvariantCulture.NumberFormat);

			XmlElement el = xmlDoc_.CreateElement(nodeName_);
			el.Attributes.Append(att1);
			el.Attributes.Append(att2);

			return el;
		}


	}
}
