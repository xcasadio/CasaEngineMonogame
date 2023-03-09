using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;
using CasaEngine.Framework.Assets.Graphics2D;
using Microsoft.Xna.Framework.Content;

namespace CasaEngine.Framework.Graphics2D
{
    public class Asset2DManager
    {

        private readonly Dictionary<int, Sprite2D> _sprite2DList = new();
        private readonly Dictionary<int, Animation2D> _animation2DList = new();
        private readonly List<int> _sprite2DLoadingList = new();
        //private Game _Game = null;

        public void ClearLoadingList()
        {
            foreach (var id in _sprite2DLoadingList)
            {
                _sprite2DList[id].UnloadTexture();
            }

            _sprite2DLoadingList.Clear();
        }

        public void LoadLoadingList(ContentManager content)
        {
            foreach (var id in _sprite2DLoadingList)
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

            foreach (var frame in anim2D.GetFrames())
            {
                AddSprite2DToLoadingList(frame.SpriteId);
            }
        }

        public void Load(XmlElement el, SaveOption option)
        {
            var version = uint.Parse(el.Attributes["version"].Value);

            var sprite2DListNode = el.SelectSingleNode("Sprite2DList");

            foreach (XmlNode node in sprite2DListNode.ChildNodes)
            {
                AddSprite2D(new Sprite2D((XmlElement)node, option));
            }

            var animation2DListNode = el.SelectSingleNode("Animation2DList");

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
            foreach (var pair in _animation2DList)
            {
                if (pair.Value.Name.Equals(name, compare))
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

#if EDITOR
        private static readonly uint Version = 1;

        public event EventHandler<Asset2DEventArg> AnimationAdded;
        public event EventHandler<Asset2DEventArg> AnimationRemoved;
        public event EventHandler<Asset2DRenamedEventArg> AnimationRenamed;
        public event EventHandler<Asset2DEventArg> SpriteAdded;
        public event EventHandler<Asset2DEventArg> SpriteRemoved;
        public event EventHandler<Asset2DRenamedEventArg> SpriteRenamed;

        public string[] GetAllSprite2DName()
        {
            var res = new List<string>();

            /*foreach(KeyValuePair<uint, Sprite2D> pair in _Sprite2DList)
			{
				res.Add(pair.Value.Name);
			}*/

            return res.ToArray();
        }

        public void DeleteSprite2DByName(string name)
        {
            /*foreach (KeyValuePair<uint, Sprite2D> pair in _Sprite2DList)
			{
				if (pair.Value.Name.Equals(name_) == true)
				{
					_Sprite2DList.IsRemoved(pair.Key);
					break;
				}
			}

            if (SpriteRemoved != null)
            {
                SpriteRemoved.Invoke(this, new Asset2DEventArg(name_));
            }*/
        }

        public bool RenameSprite2D(string sprite2DName, string newName)
        {
            /*foreach (KeyValuePair<uint, Sprite2D> pair in _Sprite2DList)
			{
				if (pair.Value.Name.Equals(sprite2DName_) == true)
				{
					//pair.Value.Name = newName_;
					_Sprite2DList[pair.Key] = pair.Value;

                    if (SpriteRenamed != null)
                    {
                        SpriteRenamed.Invoke(this, new Asset2DRenamedEventArg(sprite2DName_, newName_));
                    }

                    return true;
				}
			}*/

            return false;
        }

        public bool IsValidSprite2DName(string name)
        {
            /*foreach (KeyValuePair<uint, Sprite2D> pair in  _Sprite2DList)
			{
				if (pair.Value.Name.Equals(name_) == true)
				{
					return false;
				}
			}*/

            return true;
        }

        public void ReplaceSprite2D(Sprite2D sprite2D)
        {
            var id = int.MaxValue;

            /*foreach (KeyValuePair<uint, Sprite2D> pair in _Sprite2DList)
            {
                if (pair.Value.Name.Equals(sprite2D_.Name) == true)
                {
                    id = pair.Key;
                    break;
                }
            }*/

            if (id != int.MaxValue)
            {
                _sprite2DList[id] = sprite2D;
                //TODO : perforce checkout
            }
        }

        public string[] GetAllAnimation2DName()
        {
            var res = new List<string>();

            foreach (var pair in _animation2DList)
            {
                res.Add(pair.Value.Name);
            }

            return res.ToArray();
        }

        /*public Animation2D GetAnimation2DByName(string name_)
		{
			foreach (KeyValuePair<string, Animation2D> pair in _Animation2DList)
			{
				if (pair.Value.Name.Equals(name_))
				{
					return pair.Value;
				}
			}

			return null;
		}*/

        public bool IsValidAnimation2DName(string name)
        {
            name = name.ToLower();

            foreach (var pair in _animation2DList)
            {
                if (pair.Value.Name.ToLower().Equals(name))
                {
                    return false;
                }
            }

            return true;
        }

        public bool RenameAnimation2D(string anim2DName, string newName)
        {
            var tmp = GetAnimation2DByName(anim2DName);

            if (tmp == null)
            {
                throw new ArgumentException("Asset2DManager.RenameAnimation2D() : animation named " + anim2DName + "doesn't exist.");
            }

            if (IsValidAnimation2DName(newName) == false)
            {
                return false;
                //throw new ArgumentException("Asset2DManager.RenameAnimation2D() : animation named " + newName_ + " already exist.");
            }

            tmp.Name = newName;
            _animation2DList[tmp.Id] = tmp;

            if (AnimationRenamed != null)
            {
                AnimationRenamed.Invoke(this, new Asset2DRenamedEventArg(anim2DName, newName));
            }

            return true;
        }

        public void DeleteAnimation2DByName(string name)
        {
            foreach (var pair in _animation2DList)
            {
                if (pair.Value.Name.Equals(name))
                {
                    _animation2DList.Remove(pair.Key);

                    if (AnimationRemoved != null)
                    {
                        AnimationRemoved.Invoke(this, new Asset2DEventArg(name));
                    }

                    break;
                }
            }
        }

        public void DeleteAnimation2DById(int id)
        {
            var name = _animation2DList[id].Name;
            _animation2DList.Remove(id);

            if (AnimationRemoved != null)
            {
                AnimationRemoved.Invoke(this, new Asset2DEventArg(name));
            }
        }

        public void Save(XmlElement el, SaveOption option)
        {
            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());

            var sprite2DListNode = el.OwnerDocument.CreateElement("Sprite2DList");
            el.AppendChild(sprite2DListNode);

            foreach (var pair in _sprite2DList)
            {
                var spriteNode = el.OwnerDocument.CreateElement("Sprite2D");
                sprite2DListNode.AppendChild(spriteNode);
                pair.Value.Save(spriteNode, option);
            }

            var anim2DListNode = el.OwnerDocument.CreateElement("Animation2DList");
            el.AppendChild(anim2DListNode);

            foreach (var pair in _animation2DList)
            {
                var animNode = el.OwnerDocument.CreateElement("Animation2D");
                anim2DListNode.AppendChild(animNode);
                pair.Value.Save(animNode, option);
            }
        }

        public void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write(Version);
            bw.Write(_sprite2DList.Count);

            foreach (var pair in _sprite2DList)
            {
                pair.Value.Save(bw, option);
            }

            bw.Write(_animation2DList.Count);

            foreach (var pair in _animation2DList)
            {
                pair.Value.Save(bw, option);
            }
        }

        public void Clear()
        {
            _animation2DList.Clear();
            _sprite2DList.Clear();
        }
#endif
    }
}
