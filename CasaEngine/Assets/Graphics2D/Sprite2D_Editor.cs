using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using CasaEngineCommon.Extension;
using System.ComponentModel;
using System.Xml;
using CasaEngine.Core.Math.Shape2D;
using CasaEngine.Entities;
using CasaEngineCommon.Design;


namespace CasaEngine.Assets.Graphics2D
{
    public partial class Sprite2D
    {

        private static readonly uint Version = 2;
        public event PropertyChangedEventHandler PropertyChanged;



        [Category("Sprite"),
        ReadOnly(true)]
        public List<string> AssetFileNames => _assetFileNames;


        public Sprite2D(Texture2D tex, string assetFileName)
            : this(tex)
        {
            _assetFileNames.Add(assetFileName);
        }




        public List<KeyValuePair<string, Vector2>> GetSockets()
        {
            return _sockets.ToList();
        }

        public Vector2 GetSocketByIndex(int index)
        {
            return _sockets.ElementAt(index).Value;
        }

        public bool IsValidSocketName(string name)
        {
            return !_sockets.ContainsKey(name);
        }

        public void AddSocket(string name, Vector2 position)
        {
            _sockets.Add(name, position);
        }

        public void ModifySocket(string name, Vector2 position)
        {
            _sockets[name] = position;
        }

        public void ModifySocket(int index, Vector2 position)
        {
            _sockets[_sockets.ElementAt(index).Key] = position;
        }

        public void RemoveSocket(string name)
        {
            _sockets.Remove(name);
        }



        public void AddCollision(Shape2DObject coll)
        {
            _collisions.Add(coll);
        }

        public void SetCollisionAt(int index, Shape2DObject coll)
        {
            _collisions[index] = coll;
        }

        public void RemoveCollision(Shape2DObject coll)
        {
            if (_collisions.Remove(coll) == false)
            {
                throw new InvalidOperationException("Sprite2D.RemoveCollision() : can't remove the collision");
            }
        }

        public void RemoveCollisionAt(int index)
        {
            _collisions.RemoveAt(index);
        }


        public bool CompareTo(BaseObject other)
        {
            if (other is Sprite2D)
            {
                var s = (Sprite2D)other;

                if (_positionInTexture != s._positionInTexture
                    || _origin != s._origin
                    || _assetFileNames.Count != s._assetFileNames.Count
                    || _collisions.Count != s._collisions.Count
                    || _sockets.Count != s._sockets.Count)
                {
                    return false;
                }

                for (var i = 0; i < _assetFileNames.Count; i++)
                {
                    if (_assetFileNames[i].Equals(s._assetFileNames[i]) == false)
                    {
                        return false;
                    }
                }

                for (var i = 0; i < _collisions.Count; i++)
                {
                    if (_collisions[i].CompareTo(s._collisions[i]) == false)
                    {
                        return false;
                    }
                }

                foreach (var pair in _sockets)
                {
                    if (s._sockets.ContainsKey(pair.Key))
                    {
                        if (s._sockets[pair.Key].Equals(pair.Value) == false)
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

        private void NotifyPropertyChanged(string info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }


        public override void Save(XmlElement el, SaveOption option)
        {
            XmlElement node;

            base.Save(el, option);

            var rootNode = el.OwnerDocument.CreateElement("Sprite2D");
            el.AppendChild(rootNode);
            el.OwnerDocument.AddAttribute(rootNode, "version", Version.ToString());

            var assetNode = el.OwnerDocument.CreateElement("AssetFiles");
            rootNode.AppendChild(assetNode);

            foreach (var file in _assetFileNames)
            {
                assetNode.AppendChild(el.OwnerDocument.CreateElementWithText("AssetFileName", file));
            }

            rootNode.AppendChild(el.OwnerDocument.CreateElement("HotSpot", _origin));
            rootNode.AppendChild(el.OwnerDocument.CreateElement("PositionInTexture", _positionInTexture));

            //Collisions
            var collNode = el.OwnerDocument.CreateElement("CollisionList");
            rootNode.AppendChild(collNode);

            foreach (var col in _collisions)
            {
                node = el.OwnerDocument.CreateElement("Shape");
                col.Save(node, option);
                collNode.AppendChild(node);
            }

            //Sockets
            var socketNode = el.OwnerDocument.CreateElement("SocketList");
            rootNode.AppendChild(socketNode);

            foreach (var pair in _sockets)
            {
                node = el.OwnerDocument.CreateElement("Socket");
                node.AppendChild(el.OwnerDocument.CreateElementWithText("Name", pair.Key));
                node.AppendChild(el.OwnerDocument.CreateElement("position", pair.Value));
                socketNode.AppendChild(node);
            }
        }

        public override void Save(BinaryWriter bw, SaveOption option)
        {
            base.Save(bw, option);

            bw.Write(Version);

            bw.Write(_assetFileNames.Count);
            bw.Write(_assetFileNames.Count);
            foreach (var assetFile in _assetFileNames)
            {
                bw.Write(assetFile);
            }

            bw.Write(_origin);
            bw.Write(_positionInTexture);

            //Collisions
            bw.Write(_collisions.Count);
            foreach (var col in _collisions)
            {
                col.Save(bw, option);
            }

            //Sockets
            bw.Write(_sockets.Count);
            foreach (var pair in _sockets)
            {
                bw.Write(pair.Key);
                bw.Write(pair.Value);
            }
        }


    }
}
