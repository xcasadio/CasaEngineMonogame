using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Xna.Framework;
using System.Xml;
using Microsoft.Xna.Framework.Content;
using CasaEngine.Graphics2D;
using CasaEngine;
using CasaEngineCommon.Pool;
using CasaEngineCommon.Design;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Graphics2D
{
    /// <summary>
    /// 
    /// </summary>
    public
#if EDITOR
    partial
#endif
    class Asset2DManager
    {

        private Dictionary<int, Sprite2D> m_Sprite2DList = new Dictionary<int, Sprite2D>();
        private Dictionary<int, Animation2D> m_Animation2DList = new Dictionary<int, Animation2D>();
        private List<int> m_Sprite2DLoadingList = new List<int>();
        //private Game m_Game = null;







        /// <summary>
        /// Unload all loaded texture
        /// </summary>
        public void ClearLoadingList()
        {
            foreach (int id in m_Sprite2DLoadingList)
            {
                m_Sprite2DList[id].UnloadTexture();
            }

            m_Sprite2DLoadingList.Clear();
        }

        /// <summary>
        /// Load all textures
        /// </summary>
        /// <param name="game_"></param>
        public void LoadLoadingList(ContentManager content_)
        {
            foreach (int id in m_Sprite2DLoadingList)
            {
                m_Sprite2DList[id].LoadTexture(content_);
            }

            m_Sprite2DLoadingList.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spriteId_"></param>
        public void AddSprite2DToLoadingList(int spriteId_)
        {
            if (m_Sprite2DList.ContainsKey(spriteId_) == false)
            {
                throw new ArgumentException("Asset2DManager.AddSprite2DToLoadingList() : Sprite2D with the id " + spriteId_ + " doesn't exist.");
            }

            if (m_Sprite2DLoadingList.Contains(spriteId_) == false)
            {
                m_Sprite2DLoadingList.Add(spriteId_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite2D_"></param>
        public void AddSprite2DToLoadingList(Sprite2D sprite2D_)
        {
            if (sprite2D_ == null)
            {
                throw new ArgumentException("Asset2DManager.AddSprite2DToLoadingList() : Sprite2D is null.");
            }

            AddSprite2DToLoadingList(sprite2D_.ID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="anim2D_"></param>
        public void AddSprite2DToLoadingList(Animation2D anim2D_)
        {
            if (anim2D_ == null)
            {
                throw new ArgumentException("Asset2DManager.AddSprite2DToLoadingList() : Animation2D is null.");
            }

            foreach (Frame2D frame in anim2D_.GetFrames())
            {
                AddSprite2DToLoadingList(frame.spriteID);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        public void Load(XmlElement el_, SaveOption option_)
        {
            uint version = uint.Parse(el_.Attributes["version"].Value);

            XmlNode sprite2DListNode = el_.SelectSingleNode("Sprite2DList");

            foreach (XmlNode node in sprite2DListNode.ChildNodes)
            {
                AddSprite2D(new Sprite2D((XmlElement)node, option_));
            }

            XmlNode animation2DListNode = el_.SelectSingleNode("Animation2DList");

            foreach (XmlNode node in animation2DListNode.ChildNodes)
            {
                AddAnimation2D(new Animation2D((XmlElement)node, option_));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id_"></param>
        /// <returns>the original sprite 2D</returns>
        public Sprite2D GetSprite2DByID(int id_)
        {
            return m_Sprite2DList[id_];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id_"></param>
        /// <returns>A copy from the original sprite 2D</returns>
        public Sprite2D GetSprite2DByName(string name_, StringComparison compare_ = StringComparison.InvariantCultureIgnoreCase)
        {
            /*foreach (KeyValuePair<uint, Sprite2D> pair in m_Sprite2DList)
			{
                if (pair.Value.Name.Equals(name_, compare_) == true)
				{
                    return pair.Value;
				}
			}*/

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <returns></returns>
        public Animation2D GetAnimation2DByName(string name_, StringComparison compare_ = StringComparison.InvariantCultureIgnoreCase)
        {
            foreach (KeyValuePair<int, Animation2D> pair in m_Animation2DList)
            {
                if (pair.Value.Name.Equals(name_, compare_) == true)
                {
                    return pair.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite2D_"></param>
#if EDITOR
        public
#else
		private
#endif
        void AddSprite2D(Sprite2D sprite2D_)
        {
            m_Sprite2DList.Add(sprite2D_.ID, sprite2D_);

            //TODO : Perforce

#if EDITOR
            /*if (SpriteAdded != null)
            {
                SpriteAdded.Invoke(this, new Asset2DEventArg(sprite2D_.Name));
            }*/
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="anim_"></param>
#if EDITOR
        public
#else
		private
#endif
        void AddAnimation2D(Animation2D anim_)
        {
            m_Animation2DList.Add(anim_.ID, anim_);

#if EDITOR
            if (AnimationAdded != null)
            {
                AnimationAdded.Invoke(this, new Asset2DEventArg(anim_.Name));
            }
#endif
        }

    }
}
