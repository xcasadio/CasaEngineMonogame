using System.ComponentModel;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;
using CasaEngine.Core.Maths.Shape2D;
using CasaEngine.Editor.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Project;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Graphics2D
{
    public class Sprite2D : Entity
#if EDITOR
         , INotifyPropertyChanged, IAssetable
#endif
    {
        //constant
        private Texture2D _texture2D;
        private Rectangle _positionInTexture;
        private Point _origin = Point.Zero;

#if EDITOR
        private readonly List<Shape2DObject> _collisions = new();
#else
        private Shape2DObject[] _Collisions;
#endif

        private readonly Dictionary<string, Vector2> _sockets = new();

        private readonly List<string> _assetFileNames = new();

#if EDITOR
        [Browsable(false)]
#endif
        public Texture2D Texture2D
        {
            get { return _texture2D; }
            internal set
            {
                _texture2D = value;

#if EDITOR                
                if (_texture2D != null)
                {
                    PositionInTexture = new Rectangle(0, 0, _texture2D.Width, _texture2D.Height);
                }
#endif
            }
        }

#if EDITOR
        [Browsable(false)]
#endif
        public Rectangle PositionInTexture
        {
            get { return _positionInTexture; }
            set
            {
                _positionInTexture = value;
#if EDITOR
                NotifyPropertyChanged("PositionInTexture");
#endif
            }
        }

#if EDITOR
        [Category("Sprite")]
#endif
        public Point HotSpot
        {
            get { return _origin; }
            set
            {
                //if (value != this._Origin)
                {
                    _origin = value;
#if EDITOR
                    NotifyPropertyChanged("HotSpot");
#endif
                }
            }
        }

#if EDITOR
        [Browsable(false)]
#endif
        public Shape2DObject[] Collisions
        {
            get
            {
#if EDITOR
                return _collisions.ToArray();
#else
                return _Collisions;
#endif
            }
        }

        internal Sprite2D() { }

        public Sprite2D(Texture2D tex)
        {
            Texture2D = tex;
        }

        public Sprite2D(XmlElement node, SaveOption option)
        {
            Load(node, option);
        }

        public Sprite2D(Sprite2D sprite)
        {
            CopyFrom(sprite);
        }

        public Entity Clone()
        {
            return new Sprite2D(this);
        }

#if EDITOR
        public
#else
        internal
#endif
        void CopyFrom(Sprite2D sprite)
        {
            if (sprite == null)
            {
                throw new ArgumentNullException("Sprite2D.Copy() : Sprite2D is null");
            }

#if EDITOR
            _collisions.Clear();

            foreach (var o in sprite._collisions)
            {
                _collisions.Add(o.Clone());
            }
#else
            if (sprite._Collisions != null)
            {
                this._Collisions = sprite._Collisions;
                //this._Collisions = (Shape2DObject[])sprite_._Collisions.Clone();
            }
            else
            {
                this._Collisions = null;
            }
#endif

            _origin = sprite._origin;
            _assetFileNames.AddRange(sprite._assetFileNames);
            _positionInTexture = sprite._positionInTexture;

            base.CopyFrom(sprite);
        }

        public override void Load(XmlElement element, SaveOption option)
        {
            base.Load(element, option);

            var rootNode = element.SelectSingleNode("Sprite2D");

            var version = uint.Parse(rootNode.Attributes["version"].Value);

            if (version == 1)
            {
                _assetFileNames.Add(rootNode.SelectSingleNode("AssetFileName").InnerText);
            }
            else
            {
                foreach (XmlNode child in rootNode.SelectNodes("AssetFiles/AssetFileName"))
                {
                    _assetFileNames.Add(child.InnerText);
                }
            }

            ((XmlElement)rootNode.SelectSingleNode("PositionInTexture")).Read(ref _positionInTexture);
            ((XmlElement)rootNode.SelectSingleNode("HotSpot")).Read(ref _origin);

            var collisionNode = rootNode.SelectSingleNode("CollisionList");

#if EDITOR
            _collisions.Clear();

            foreach (XmlNode node in collisionNode.ChildNodes)
            {
                _collisions.Add(Shape2DObject.CreateShape2DObject((XmlElement)node, option));
            }
#else
            if (collisionNode.ChildNodes.Count > 0)
            {
                _Collisions = new Shape2DObject[collisionNode.ChildNodes.Count];
                int i = 0;

                foreach (XmlNode node in collisionNode.ChildNodes)
                {
                    _Collisions[i++] = (Shape2DObject.CreateShape2DObject((XmlElement)node, option));
                }
            }
            else
            {
                _Collisions = null;
            }
#endif

            //Sockets
            var socketNode = rootNode.SelectSingleNode("SocketList");

            foreach (XmlNode node in socketNode.ChildNodes)
            {
                var position = new Vector2();
                ((XmlElement)node.SelectSingleNode("position")).Read(ref position);
                _sockets.Add(node.SelectSingleNode("Name").InnerText, position);
            }
        }

        public override void Load(BinaryReader br, SaveOption option)
        {
            base.Load(br, option);
            throw new NotImplementedException();
        }

        public void UnloadTexture()
        {
#if !EDITOR
            if (_texture2D == null)
            {
                throw new InvalidOperationException("Sprite2D.LoadTexture() : texture is null !");
            }
#endif

            //_Texture2D.Dispose();
            _texture2D = null;
        }

        public Vector2 GetSocketByName(string name)
        {
            return _sockets[name];
        }

        public void LoadTexture(ContentManager content)
        {
            if (_texture2D != null && _texture2D.IsDisposed == false)
            {
                return;
            }

            var assetFile = Path.GetDirectoryName(_assetFileNames[0]) + Path.DirectorySeparatorChar +
                            Path.GetFileNameWithoutExtension(_assetFileNames[0]);

            _texture2D = content.Load<Texture2D>(assetFile);
        }

        public void LoadTextureFile(GraphicsDevice device)
        {
            string assetFile;

#if EDITOR
            assetFile = Game.EngineComponents.ProjectManager.ProjectPath + Path.DirectorySeparatorChar +
                ProjectManager.AssetDirPath + Path.DirectorySeparatorChar + _assetFileNames[0];
#else
            assetFile = Game.EngineComponents.Game.Content.RootDirectory + System.IO.Path.DirectorySeparatorChar + _assetFileNames[0];
#endif

            if (_texture2D != null
                && _texture2D.IsDisposed == false
                && _texture2D.GraphicsDevice.IsDisposed == false)
            {
                return;
            }

            _texture2D = Texture2D.FromStream(device, File.OpenRead(assetFile));
        }

#if EDITOR
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

        public bool CompareTo(Entity other)
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
#endif
    }
}
