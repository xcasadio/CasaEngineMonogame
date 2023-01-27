using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using CasaEngine;
using CasaEngine.Graphics2D;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Graphics2D
{
	/// <summary>
	/// 
	/// </summary>
	public partial class Asset2DManager
	{
		#region Fields

        static private readonly uint m_Version = 1;

        public event EventHandler<Asset2DEventArg> AnimationAdded;
        public event EventHandler<Asset2DEventArg> AnimationRemoved;
        public event EventHandler<Asset2DRenamedEventArg> AnimationRenamed;
        public event EventHandler<Asset2DEventArg> SpriteAdded;
        public event EventHandler<Asset2DEventArg> SpriteRemoved;
        public event EventHandler<Asset2DRenamedEventArg> SpriteRenamed;

		#endregion

		#region Properties

		#endregion

		#region Constructors

		#endregion

		#region Methods

        #region Sprite2D

        /// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public string[] GetAllSprite2DName()
		{
			List<string> res = new List<string>();

			/*foreach(KeyValuePair<uint, Sprite2D> pair in m_Sprite2DList)
			{
				res.Add(pair.Value.Name);
			}*/

			return res.ToArray();
		}

        /// <summary>
		/// 
		/// </summary>
		/// <param name="name_"></param>
		public void DeleteSprite2DByName(string name_)
		{
			/*foreach (KeyValuePair<uint, Sprite2D> pair in m_Sprite2DList)
			{
				if (pair.Value.Name.Equals(name_) == true)
				{
					m_Sprite2DList.Remove(pair.Key);
					break;
				}
			}

            if (SpriteRemoved != null)
            {
                SpriteRemoved.Invoke(this, new Asset2DEventArg(name_));
            }*/
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sprite2DName_"></param>
		/// <param name="newName_"></param>
		/// <returns></returns>
		public bool RenameSprite2D(string sprite2DName_, string newName_)
		{
			/*foreach (KeyValuePair<uint, Sprite2D> pair in m_Sprite2DList)
			{
				if (pair.Value.Name.Equals(sprite2DName_) == true)
				{
					//pair.Value.Name = newName_;
					m_Sprite2DList[pair.Key] = pair.Value;

                    if (SpriteRenamed != null)
                    {
                        SpriteRenamed.Invoke(this, new Asset2DRenamedEventArg(sprite2DName_, newName_));
                    }

                    return true;
				}
			}*/

            return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name_"></param>
		/// <returns>true if the name is unsused, else returns false</returns>
		public bool IsValidSprite2DName(string name_)
		{
			/*foreach (KeyValuePair<uint, Sprite2D> pair in  m_Sprite2DList)
			{
				if (pair.Value.Name.Equals(name_) == true)
				{
					return false;
				}
			}*/

			return true;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite2D_"></param>
        public void ReplaceSprite2D(Sprite2D sprite2D_)
        {
            int id = int.MaxValue;

            /*foreach (KeyValuePair<uint, Sprite2D> pair in m_Sprite2DList)
            {
                if (pair.Value.Name.Equals(sprite2D_.Name) == true)
                {
                    id = pair.Key;
                    break;
                }
            }*/

            if (id != int.MaxValue)
            {
                m_Sprite2DList[id] = sprite2D_;
                //TODO : perforce checkout
            }
        }

        #endregion

        #region Animation2D

        /// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public string[] GetAllAnimation2DName()
		{
			List<string> res = new List<string>();

			foreach (KeyValuePair<int, Animation2D> pair in m_Animation2DList)
			{
				res.Add(pair.Value.Name);
			}

			return res.ToArray();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name_"></param>
		/// <returns></returns>
		/*public Animation2D GetAnimation2DByName(string name_)
		{
			foreach (KeyValuePair<string, Animation2D> pair in m_Animation2DList)
			{
				if (pair.Value.Name.Equals(name_))
				{
					return pair.Value;
				}
			}

			return null;
		}*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name_"></param>
		/// <returns>true if the name is unsused, else returns false</returns>
		public bool IsValidAnimation2DName(string name_)
		{
            string name = name_.ToLower();

			foreach (KeyValuePair<int, Animation2D> pair in m_Animation2DList)
			{
                if (pair.Value.Name.ToLower().Equals(name) == true)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="anim2DName_"></param>
		/// <param name="newName_"></param>
		public bool RenameAnimation2D(string anim2DName_, string newName_)
		{
			Animation2D tmp = GetAnimation2DByName(anim2DName_);

			if (tmp == null)
			{
				throw new ArgumentException("Asset2DManager.RenameAnimation2D() : animation named " + anim2DName_ + "doesn't exist.");
			}

			if (IsValidAnimation2DName(newName_) == false)
			{
                return false;
                //throw new ArgumentException("Asset2DManager.RenameAnimation2D() : animation named " + newName_ + " already exist.");
			}

			tmp.Name = newName_;
			m_Animation2DList[tmp.ID] = tmp;

            if (AnimationRenamed != null)
            {
                AnimationRenamed.Invoke(this, new Asset2DRenamedEventArg(anim2DName_, newName_));
            }

            return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name_"></param>
		public void DeleteAnimation2DByName(string name_)
		{
			foreach (KeyValuePair<int, Animation2D> pair in m_Animation2DList)
			{
				if (pair.Value.Name.Equals(name_) == true)
				{
					m_Animation2DList.Remove(pair.Key);

                    if (AnimationRemoved != null)
                    {
                        AnimationRemoved.Invoke(this, new Asset2DEventArg(name_));
                    }

					break;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id_"></param>
		public void DeleteAnimation2DByID(int id_)
		{
            string name = m_Animation2DList[id_].Name;
			m_Animation2DList.Remove(id_);

            if (AnimationRemoved != null)
            {
                AnimationRemoved.Invoke(this, new Asset2DEventArg(name));
            }
		}

        #endregion		

		/// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
        public void Save(XmlElement el_, SaveOption option_)
		{
			el_.OwnerDocument.AddAttribute(el_, "version", m_Version.ToString());

			XmlElement sprite2DListNode = el_.OwnerDocument.CreateElement("Sprite2DList");
			el_.AppendChild(sprite2DListNode);

			foreach (KeyValuePair<int, Sprite2D> pair in m_Sprite2DList)
			{
				XmlElement spriteNode = el_.OwnerDocument.CreateElement("Sprite2D");
				sprite2DListNode.AppendChild(spriteNode);
                pair.Value.Save(spriteNode, option_);
			}

			XmlElement anim2DListNode = el_.OwnerDocument.CreateElement("Animation2DList");
			el_.AppendChild(anim2DListNode);

			foreach (KeyValuePair<int, Animation2D> pair in m_Animation2DList)
			{
				XmlElement animNode = el_.OwnerDocument.CreateElement("Animation2D");
				anim2DListNode.AppendChild(animNode);
				pair.Value.Save(animNode, option_);
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write(m_Version);
            bw_.Write(m_Sprite2DList.Count);

            foreach (KeyValuePair<int, Sprite2D> pair in m_Sprite2DList)
            {
                pair.Value.Save(bw_, option_);
            }

            bw_.Write(m_Animation2DList.Count);

            foreach (KeyValuePair<int, Animation2D> pair in m_Animation2DList)
            {
                pair.Value.Save(bw_, option_);
            }
        }

		/// <summary>
		/// 
		/// </summary>
		public void Clear()
		{
			m_Animation2DList.Clear();
			m_Sprite2DList.Clear();
		}

		#endregion
	}
}
