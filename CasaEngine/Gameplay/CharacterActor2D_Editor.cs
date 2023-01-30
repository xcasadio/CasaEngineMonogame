using System.Xml;
using CasaEngine.Game;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Gameplay
{
    public partial class CharacterActor2D
    {







        public override void Save(XmlElement el_, SaveOption opt_)
        {
            base.Save(el_, opt_);

            XmlElement statusNode = el_.OwnerDocument.CreateElement("Status");
            el_.AppendChild(statusNode);
            el_.OwnerDocument.AddAttribute(statusNode, "speed", Speed.ToString());
            el_.OwnerDocument.AddAttribute(statusNode, "strength", Strength.ToString());
            el_.OwnerDocument.AddAttribute(statusNode, "defense", Defense.ToString());
            el_.OwnerDocument.AddAttribute(statusNode, "HPMax", HPMax.ToString());
            el_.OwnerDocument.AddAttribute(statusNode, "MPMax", MPMax.ToString());

            XmlElement animNode;
            XmlElement animListNode = el_.OwnerDocument.CreateElement("AnimationList");
            el_.AppendChild(animListNode);

            //foreach (KeyValuePair<int, Animation2D> pair in m_Animations)
            foreach (KeyValuePair<int, string> pair in m_AnimationListToLoad)
            {
                animNode = el_.OwnerDocument.CreateElement("Animation");
                animListNode.AppendChild(animNode);
                el_.OwnerDocument.AddAttribute(animNode, "index", pair.Key.ToString());
                el_.OwnerDocument.AddAttribute(animNode, "name", pair.Value);
            }
        }

        public override void Save(BinaryWriter bw_, SaveOption opt_)
        {
            base.Save(bw_, opt_);

            bw_.Write(Speed);
            bw_.Write(Strength);
            bw_.Write(Defense);
            bw_.Write(HPMax);
            bw_.Write(MPMax);

            bw_.Write(m_Animations.Count);

            foreach (KeyValuePair<int, Animation2D> pair in m_Animations)
            {
                bw_.Write(pair.Key);
                bw_.Write(pair.Value.Name);
            }
        }

        public void AddOrSetAnimation(int index_, string name_)
        {
            Animation2D anim = Engine.Instance.ObjectManager.GetObjectByPath(name_) as Animation2D;

            if (anim != null)
            {
                if (m_AnimationListToLoad.ContainsKey(index_))
                {
                    m_Animations[index_] = anim;
                    m_AnimationListToLoad[index_] = anim.Name;
                }
                else
                {
                    m_Animations.Add(index_, anim);
                    m_AnimationListToLoad.Add(index_, anim.Name);
                }
            }
        }

        public int AddAnimation(string name_)
        {
            int index = 0;

            while (m_AnimationListToLoad.ContainsKey(index) == true)
            {
                index++;
            }

            Animation2D anim = Engine.Instance.Asset2DManager.GetAnimation2DByName(name_);

            if (anim != null)
            {
                m_Animations.Add(index, anim);
                m_AnimationListToLoad.Add(index, anim.Name);
                return index;
            }

            return -1;
        }

        public string GetAnimationName(int index_)
        {
            //if (m_Animations.ContainsKey(index_))
            if (m_AnimationListToLoad.ContainsKey(index_))
            {
                //return m_Animations[index_].Name;
                return m_AnimationListToLoad[index_];
            }

            return null;
        }

        public List<string> GetAllAnimationName()
        {
            List<string> res = new List<string>();

            //foreach (KeyValuePair<int, Animation2D> pair in m_Animations)
            foreach (KeyValuePair<int, string> pair in m_AnimationListToLoad)
            {
                res.Add(pair.Value); //pair.Value.Name
            }

            return res;
        }

        public override bool CompareTo(BaseObject other_)
        {
            throw new NotImplementedException();
        }

    }
}
