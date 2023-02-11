using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Graphics2D
{
    public partial class Asset2DManager
    {

        static private readonly uint Version = 1;

        public event EventHandler<Asset2DEventArg> AnimationAdded;
        public event EventHandler<Asset2DEventArg> AnimationRemoved;
        public event EventHandler<Asset2DRenamedEventArg> AnimationRenamed;
        public event EventHandler<Asset2DEventArg> SpriteAdded;
        public event EventHandler<Asset2DEventArg> SpriteRemoved;
        public event EventHandler<Asset2DRenamedEventArg> SpriteRenamed;








        public string[] GetAllSprite2DName()
        {
            List<string> res = new List<string>();

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
					_Sprite2DList.Remove(pair.Key);
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
            int id = int.MaxValue;

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
            List<string> res = new List<string>();

            foreach (KeyValuePair<int, Animation2D> pair in _animation2DList)
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
            string name = name_.ToLower();

            foreach (KeyValuePair<int, Animation2D> pair in _animation2DList)
            {
                if (pair.Value.Name.ToLower().Equals(name) == true)
                {
                    return false;
                }
            }

            return true;
        }

        public bool RenameAnimation2D(string anim2DName, string newName)
        {
            Animation2D tmp = GetAnimation2DByName(anim2DName);

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
            _animation2DList[tmp.ID] = tmp;

            if (AnimationRenamed != null)
            {
                AnimationRenamed.Invoke(this, new Asset2DRenamedEventArg(anim2DName, newName));
            }

            return true;
        }

        public void DeleteAnimation2DByName(string name)
        {
            foreach (KeyValuePair<int, Animation2D> pair in _animation2DList)
            {
                if (pair.Value.Name.Equals(name) == true)
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
            string name = _animation2DList[id].Name;
            _animation2DList.Remove(id);

            if (AnimationRemoved != null)
            {
                AnimationRemoved.Invoke(this, new Asset2DEventArg(name));
            }
        }


        public void Save(XmlElement el, SaveOption option)
        {
            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());

            XmlElement sprite2DListNode = el.OwnerDocument.CreateElement("Sprite2DList");
            el.AppendChild(sprite2DListNode);

            foreach (KeyValuePair<int, Sprite2D> pair in _sprite2DList)
            {
                XmlElement spriteNode = el.OwnerDocument.CreateElement("Sprite2D");
                sprite2DListNode.AppendChild(spriteNode);
                pair.Value.Save(spriteNode, option);
            }

            XmlElement anim2DListNode = el.OwnerDocument.CreateElement("Animation2DList");
            el.AppendChild(anim2DListNode);

            foreach (KeyValuePair<int, Animation2D> pair in _animation2DList)
            {
                XmlElement animNode = el.OwnerDocument.CreateElement("Animation2D");
                anim2DListNode.AppendChild(animNode);
                pair.Value.Save(animNode, option);
            }
        }

        public void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write(Version);
            bw.Write(_sprite2DList.Count);

            foreach (KeyValuePair<int, Sprite2D> pair in _sprite2DList)
            {
                pair.Value.Save(bw, option);
            }

            bw.Write(_animation2DList.Count);

            foreach (KeyValuePair<int, Animation2D> pair in _animation2DList)
            {
                pair.Value.Save(bw, option);
            }
        }

        public void Clear()
        {
            _animation2DList.Clear();
            _sprite2DList.Clear();
        }

    }
}
