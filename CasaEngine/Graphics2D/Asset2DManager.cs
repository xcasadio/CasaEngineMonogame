using System.Xml;
using Microsoft.Xna.Framework.Content;
using CasaEngineCommon.Design;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Graphics2D
{
    public
#if EDITOR
    partial
#endif
    class Asset2DManager
    {

        private readonly Dictionary<int, Sprite2D> _sprite2DList = new();
        private readonly Dictionary<int, Animation2D> _animation2DList = new();
        private readonly List<int> _sprite2DLoadingList = new();
        //private Game _Game = null;







        public void ClearLoadingList()
        {
            foreach (int id in _sprite2DLoadingList)
            {
                _sprite2DList[id].UnloadTexture();
            }

            _sprite2DLoadingList.Clear();
        }

        public void LoadLoadingList(ContentManager content)
        {
            foreach (int id in _sprite2DLoadingList)
            {
                _sprite2DList[id].LoadTexture(content);
            }

            _sprite2DLoadingList.Clear();
        }

        public void AddSprite2DToLoadingList(int spriteId)
        {
            if (_sprite2DList.ContainsKey(spriteId) == false)
            {
                throw new ArgumentException("Asset2DManager.AddSprite2DToLoadingList() : Sprite2D with the id " + spriteId + " doesn't exist.");
            }

            if (_sprite2DLoadingList.Contains(spriteId) == false)
            {
                _sprite2DLoadingList.Add(spriteId);
            }
        }

        public void AddSprite2DToLoadingList(Sprite2D sprite2D)
        {
            if (sprite2D == null)
            {
                throw new ArgumentException("Asset2DManager.AddSprite2DToLoadingList() : Sprite2D is null.");
            }

            AddSprite2DToLoadingList(sprite2D.Id);
        }

        public void AddSprite2DToLoadingList(Animation2D anim2D)
        {
            if (anim2D == null)
            {
                throw new ArgumentException("Asset2DManager.AddSprite2DToLoadingList() : Animation2D is null.");
            }

            foreach (Frame2D frame in anim2D.GetFrames())
            {
                AddSprite2DToLoadingList(frame.SpriteId);
            }
        }

        public void Load(XmlElement el, SaveOption option)
        {
            uint version = uint.Parse(el.Attributes["version"].Value);

            XmlNode sprite2DListNode = el.SelectSingleNode("Sprite2DList");

            foreach (XmlNode node in sprite2DListNode.ChildNodes)
            {
                AddSprite2D(new Sprite2D((XmlElement)node, option));
            }

            XmlNode animation2DListNode = el.SelectSingleNode("Animation2DList");

            foreach (XmlNode node in animation2DListNode.ChildNodes)
            {
                AddAnimation2D(new Animation2D((XmlElement)node, option));
            }
        }

        public Sprite2D GetSprite2DById(int id)
        {
            return _sprite2DList[id];
        }

        public Sprite2D GetSprite2DByName(string name, StringComparison compare = StringComparison.InvariantCultureIgnoreCase)
        {
            /*foreach (KeyValuePair<uint, Sprite2D> pair in _Sprite2DList)
			{
                if (pair.Value.Name.Equals(name_, compare_) == true)
				{
                    return pair.Value;
				}
			}*/

            return null;
        }

        public Animation2D GetAnimation2DByName(string name, StringComparison compare = StringComparison.InvariantCultureIgnoreCase)
        {
            foreach (KeyValuePair<int, Animation2D> pair in _animation2DList)
            {
                if (pair.Value.Name.Equals(name, compare) == true)
                {
                    return pair.Value;
                }
            }

            return null;
        }

#if EDITOR
        public
#else
		private
#endif
        void AddSprite2D(Sprite2D sprite2D)
        {
            _sprite2DList.Add(sprite2D.Id, sprite2D);

            //TODO : Perforce

#if EDITOR
            /*if (SpriteAdded != null)
            {
                SpriteAdded.Invoke(this, new Asset2DEventArg(sprite2D_.Name));
            }*/
#endif
        }

#if EDITOR
        public
#else
		private
#endif
        void AddAnimation2D(Animation2D ani)
        {
            _animation2DList.Add(ani.Id, ani);

#if EDITOR
            if (AnimationAdded != null)
            {
                AnimationAdded.Invoke(this, new Asset2DEventArg(ani.Name));
            }
#endif
        }

    }
}
