
#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.IO;
using CasaEngineCommon.Extension;
using System.ComponentModel;
using System.Xml;
using CasaEngine.Math.Shape2D;
using CasaEngine;
using CasaEngineCommon.Design;
using CasaEngine.Gameplay.Actor.Object;

#endregion

namespace CasaEngine.Assets.Graphics2D
{
	/// <summary>
	/// 
	/// </summary>
	public partial class Sprite2D
	{
		#region Fields

        static private readonly uint m_Version = 2;
		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		#region Properties

		/// <summary>
		/// Gets/Sets Texture file name
		/// </summary>
        [Category("Sprite"), 
        ReadOnly(true)]
        public List<string> AssetFileNames
		{
			get { return m_AssetFileNames; }
		}

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="tex_"></param>
		/// <param name="assetFileName_"></param>
		/// <param name="textureFileName_"></param>
		public Sprite2D(Texture2D tex_, string assetFileName_)
			: this(tex_)
		{
			m_AssetFileNames.Add(assetFileName_);
		}

		#endregion

		#region Methods

        #region Socket

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<KeyValuePair<string, Vector2>> GetSockets()
        {
            return m_Sockets.ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Vector2 GetSocketByIndex(int index)
        {
            return m_Sockets.ElementAt(index).Value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <returns></returns>
        public bool IsValidSocketName(string name_)
        {
            return !m_Sockets.ContainsKey(name_);
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="name_"></param>
        /// <param name="position_"></param>
        public void AddSocket(string name_, Vector2 position_)
        {
            m_Sockets.Add(name_, position_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <param name="position_"></param>
        public void ModifySocket(string name_, Vector2 position_)
        {
            m_Sockets[name_] = position_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        /// <param name="position_"></param>
        public void ModifySocket(int index_, Vector2 position_)
        {
            m_Sockets[m_Sockets.ElementAt(index_).Key] = position_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        public void RemoveSocket(string name_)
        {
            m_Sockets.Remove(name_);
        }

        #endregion

        #region Collision

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coll_"></param>
        public void AddCollision(Shape2DObject coll_)
        {
            m_Collisions.Add(coll_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        /// <param name="coll_"></param>
        public void SetCollisionAt(int index_, Shape2DObject coll_)
        {
            m_Collisions[index_] = coll_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="coll_"></param>
        public void RemoveCollision(Shape2DObject coll_)
        {
            if (m_Collisions.Remove(coll_) == false)
            {
                throw new InvalidOperationException("Sprite2D.RemoveCollision() : can't remove the collision");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        public void RemoveCollisionAt(int index_)
        {
            m_Collisions.RemoveAt(index_);
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        /// <returns></returns>
        public override bool CompareTo(BaseObject other_)
        {
            if (other_ is Sprite2D)
            {
                Sprite2D s = (Sprite2D)other_;

                if (m_PositionInTexture != s.m_PositionInTexture
                    || m_Origin != s.m_Origin
                    || m_AssetFileNames.Count != s.m_AssetFileNames.Count
                    || m_Collisions.Count != s.m_Collisions.Count
                    || m_Sockets.Count != s.m_Sockets.Count)
                {
                    return false;
                }

                for (int i = 0; i < m_AssetFileNames.Count; i++)
                {
                    if (m_AssetFileNames[i].Equals(s.m_AssetFileNames[i]) == false)
                    {
                        return false;
                    }
                }

                for (int i = 0; i < m_Collisions.Count; i++)
                {
                    if (m_Collisions[i].CompareTo(s.m_Collisions[i]) == false)
                    {
                        return false;
                    }
                }

                foreach (var pair in m_Sockets)
                {
                    if (s.m_Sockets.ContainsKey(pair.Key))
                    {
                        if (s.m_Sockets[pair.Key].Equals(pair.Value) == false)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
		/// 
		/// </summary>
		/// <param name="info"></param>
		private void NotifyPropertyChanged(String info)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(info));
			}
		}

        #region Save

        /// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
        public override void Save(XmlElement el_, SaveOption option_)
		{
            XmlElement node;

            base.Save(el_, option_);

            XmlElement rootNode = el_.OwnerDocument.CreateElement("Sprite2D");
            el_.AppendChild(rootNode);
            el_.OwnerDocument.AddAttribute(rootNode, "version", m_Version.ToString());

            XmlElement assetNode = el_.OwnerDocument.CreateElement("AssetFiles");
            rootNode.AppendChild(assetNode);

            foreach (string file in m_AssetFileNames)
            {
                assetNode.AppendChild(el_.OwnerDocument.CreateElementWithText("AssetFileName", file));
            }

            rootNode.AppendChild(el_.OwnerDocument.CreateElement("HotSpot", m_Origin));
            rootNode.AppendChild(el_.OwnerDocument.CreateElement("PositionInTexture", m_PositionInTexture));

            //Collisions
			XmlElement collNode = el_.OwnerDocument.CreateElement("CollisionList");
            rootNode.AppendChild(collNode);

			foreach (Shape2DObject col in m_Collisions)
			{
                node = el_.OwnerDocument.CreateElement("Shape");
                col.Save(node, option_);
                collNode.AppendChild(node);
			}

            //Sockets
            XmlElement socketNode = el_.OwnerDocument.CreateElement("SocketList");
            rootNode.AppendChild(socketNode);

            foreach (var pair in m_Sockets)
            {
                node = el_.OwnerDocument.CreateElement("Socket");
                node.AppendChild(el_.OwnerDocument.CreateElementWithText("Name", pair.Key));
                node.AppendChild(el_.OwnerDocument.CreateElement("Position", pair.Value));
                socketNode.AppendChild(node);
            }
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public override void Save(BinaryWriter bw_, SaveOption option_)
        {
            base.Save(bw_, option_);

            bw_.Write(m_Version);

            bw_.Write(m_AssetFileNames.Count);
            bw_.Write(m_AssetFileNames.Count);
            foreach (string assetFile in m_AssetFileNames)
            {
                bw_.Write(assetFile);
            }

            bw_.Write(m_Origin);
            bw_.Write(m_PositionInTexture);

            //Collisions
            bw_.Write(m_Collisions.Count);
            foreach (Shape2DObject col in m_Collisions)
            {
                col.Save(bw_, option_);
            }

            //Sockets
            bw_.Write(m_Sockets.Count);
            foreach (var pair in m_Sockets)
            {
                bw_.Write(pair.Key);
                bw_.Write(pair.Value);
            }
        }

        #endregion

        #endregion
    }
}
