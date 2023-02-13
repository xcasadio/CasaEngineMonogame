using System.Xml;
using CasaEngine.Game;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;
using CasaEngine.Assets.Graphics2D;
using CasaEngine.Gameplay.Actor;

namespace CasaEngine.Gameplay
{
    public partial class CharacterActor2D
    {







        public override void Save(XmlElement el, SaveOption opt)
        {
            base.Save(el, opt);

            var statusNode = el.OwnerDocument.CreateElement("Status");
            el.AppendChild(statusNode);
            el.OwnerDocument.AddAttribute(statusNode, "speed", Speed.ToString());
            el.OwnerDocument.AddAttribute(statusNode, "strength", Strength.ToString());
            el.OwnerDocument.AddAttribute(statusNode, "defense", Defense.ToString());
            el.OwnerDocument.AddAttribute(statusNode, "HPMax", HpMax.ToString());
            el.OwnerDocument.AddAttribute(statusNode, "MPMax", MpMax.ToString());

            XmlElement animNode;
            var animListNode = el.OwnerDocument.CreateElement("AnimationList");
            el.AppendChild(animListNode);

            //foreach (KeyValuePair<int, Animation2D> pair in _Animations)
            foreach (var pair in _animationListToLoad)
            {
                animNode = el.OwnerDocument.CreateElement("Animation");
                animListNode.AppendChild(animNode);
                el.OwnerDocument.AddAttribute(animNode, "index", pair.Key.ToString());
                el.OwnerDocument.AddAttribute(animNode, "name", pair.Value);
            }
        }

        public override void Save(BinaryWriter bw, SaveOption opt)
        {
            base.Save(bw, opt);

            bw.Write(Speed);
            bw.Write(Strength);
            bw.Write(Defense);
            bw.Write(HpMax);
            bw.Write(MpMax);

            bw.Write(_animations.Count);

            foreach (var pair in _animations)
            {
                bw.Write(pair.Key);
                bw.Write(pair.Value.Name);
            }
        }

        public void AddOrSetAnimation(int index, string name)
        {
            var anim = Engine.Instance.ObjectManager.GetObjectByPath(name) as Animation2D;

            if (anim != null)
            {
                if (_animationListToLoad.ContainsKey(index))
                {
                    _animations[index] = anim;
                    _animationListToLoad[index] = anim.Name;
                }
                else
                {
                    _animations.Add(index, anim);
                    _animationListToLoad.Add(index, anim.Name);
                }
            }
        }

        public int AddAnimation(string name)
        {
            var index = 0;

            while (_animationListToLoad.ContainsKey(index))
            {
                index++;
            }

            var anim = Engine.Instance.Asset2DManager.GetAnimation2DByName(name);

            if (anim != null)
            {
                _animations.Add(index, anim);
                _animationListToLoad.Add(index, anim.Name);
                return index;
            }

            return -1;
        }

        public string GetAnimationName(int index)
        {
            //if (_Animations.ContainsKey(index_))
            if (_animationListToLoad.ContainsKey(index))
            {
                //return _Animations[index_].Name;
                return _animationListToLoad[index];
            }

            return null;
        }

        public List<string> GetAllAnimationName()
        {
            var res = new List<string>();

            //foreach (KeyValuePair<int, Animation2D> pair in _Animations)
            foreach (var pair in _animationListToLoad)
            {
                res.Add(pair.Value); //pair.Value.Name
            }

            return res;
        }

        public override bool CompareTo(Actor.BaseObject other)
        {
            throw new NotImplementedException();
        }

    }
}
