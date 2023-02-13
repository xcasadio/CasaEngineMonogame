using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using CasaEngineCommon.Extension;
using Microsoft.Xna.Framework.Content;
using System.Xml;
using CasaEngine.Core_Systems.Math.Shape2D;
using CasaEngineCommon.Design;
using CasaEngine.Game;
using CasaEngine.Gameplay.Actor;


#if EDITOR
using System.ComponentModel;
using CasaEngine.Editor.Assets;
using CasaEngine.Project;
#endif

namespace CasaEngine.Assets.Graphics2D
{
    public
#if EDITOR
    partial
#endif
    class Sprite2D
        : BaseObject
#if EDITOR
         , INotifyPropertyChanged, IAssetable
#endif
    {

        //constant
        private Texture2D _texture2D = null;
        private Rectangle _positionInTexture = new();
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



        public override BaseObject Clone()
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
            if (sprite_._Collisions != null)
            {
                this._Collisions = sprite_._Collisions;
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
                    _Collisions[i++] = (Shape2DObject.CreateShape2DObject((XmlElement)node, option_));
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
			if (_Texture2D == null)
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
            if (_texture2D != null
                && _texture2D.IsDisposed == false)
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
            assetFile = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar +
                ProjectManager.AssetDirPath + Path.DirectorySeparatorChar + _assetFileNames[0];
#else
            assetFile = Engine.Instance.Game.Content.RootDirectory + System.IO.Path.DirectorySeparatorChar + _AssetFileNames[0];
#endif

            if (_texture2D != null
                && _texture2D.IsDisposed == false
                && _texture2D.GraphicsDevice.IsDisposed == false)
            {
                return;
            }

            _texture2D = Texture2D.FromStream(device, File.OpenRead(assetFile));
        }

    }
}
