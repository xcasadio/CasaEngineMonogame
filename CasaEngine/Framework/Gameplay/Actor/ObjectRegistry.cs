using System.Xml;
using CasaEngine.Core.Extension;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Project;
using CasaEngine.Core.Design;

namespace CasaEngine.Framework.Gameplay.Actor
{
    public class ObjectRegistry
    {
#if EDITOR
        private static readonly uint Version = 1;

        private ObjectRegistryStatus CreateObjectRegistryStatus(Entity ob, bool add)
        {
            var objectRegistryStatus = new ObjectRegistryStatus();
            objectRegistryStatus.ClassName = ob.GetType().FullName;
            objectRegistryStatus.Entity = ob;
            //objectRegistryStatus.Name = ob_.Name;

            if (add)
            {
                objectRegistryStatus.Id = _unusedId++;
                ob.Id = objectRegistryStatus.Id;
            }
            else
            {
                objectRegistryStatus.Id = ob.Id;
            }

            return objectRegistryStatus;
        }

        public void AddObject(Entity obj, string name)
        {
            //obj_.Name = name_;
            var o = CreateObjectRegistryStatus(obj, true);
            _objectRegistry.Add(o);
        }

        public void AddObject(Entity obj)
        {
            //AddObject(obj_, obj_.Name);
        }

        public void RemoveObject(string name)
        {
            foreach (var o in _objectRegistry)
            {
                if (o.Name.Equals(name))
                {
                    _objectRegistry.Remove(o);
                    break;
                }
            }
        }

        public void RenameObject(string name, string newName)
        {
            if (ContainsObjectNamed(newName))
            {
                throw new Exception("_ObjectRegistry.RenameObject() : an object named " + newName + " already exist.");
            }

            if (ContainsObjectNamed(name) == false)
            {
                throw new Exception("_ObjectRegistry.RenameObject() : the object named " + name + " doesn't exist.");
            }

            for (var i = 0; i < _objectRegistry.Count; i++)
            {
                if (_objectRegistry[i].Name.Equals(name))
                {
                    var o = new ObjectRegistryStatus(_objectRegistry[i]);
                    o.Name = newName;

                    var loaded = o.IsLoaded;
                    if (o.IsLoaded == false)
                    {
                        Load(o);
                    }
                    //o.Entity.Name = newName_;
                    //Save(o);
                    if (loaded == false)
                    {
                        o.Close();
                    }

                    _objectRegistry[i] = o;
                    break;
                }
            }
        }

        public void Clear()
        {
            _objectRegistry.Clear();
        }

        public List<string> GetAllObjectName()
        {
            var res = new List<string>();

            foreach (var o in _objectRegistry)
            {
                res.Add(o.Name);
            }

            return res;
        }

        public bool IsValidName(string name)
        {
            foreach (var o in _objectRegistry)
            {
                if (o.Name.Equals(name))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetObjectIdByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("Please give a valid no empty string");
            }

            foreach (var o in _objectRegistry)
            {
                if (o.Name.Equals(name))
                {
                    return o.Id;
                }
            }

            throw new Exception("The object named " + name + " doesn't exist");
        }

        public void Save(XmlElement el, SaveOption option)
        {
            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());

            XmlDocument xmlDocLast = null;
            if (string.IsNullOrEmpty(Game.Engine.Instance.ProjectManager.ProjectFileOpened) == false
                && File.Exists(Game.Engine.Instance.ProjectManager.ProjectFileOpened))
            {
                xmlDocLast = new XmlDocument();
                xmlDocLast.Load(Game.Engine.Instance.ProjectManager.ProjectFileOpened);
            }

            foreach (var o in _objectRegistry)
            {
                var objectNode = CreateObjectNode(el, o);

                if (o.IsLoaded)
                {
                    o.Entity.Save(objectNode, SaveOption.Editor);
                }

                //Si on sauvegarde et qu'il y a deja une sauvegarde
                //On est obligé de mixer le nouveau fichier avec le nouveau
                //Car on ne sauvegarde a chaque fois que le monde courant avec ses objets chargés
                //(on ne peut pas sauvegarder les autres mondes sinon il faudrait les chargés a chaque fois)
                else
                {
                    var oldNode = xmlDocLast?.SelectSingleNode("Project/ObjectRegistry/Object[@id='" + o.Id + "']");

                    if (oldNode != null)
                    {
                        //copy attributes
                        var alreadyExist = false;
                        foreach (XmlAttribute attLast in oldNode.Attributes)
                        {
                            alreadyExist = false;
                            foreach (XmlAttribute att in objectNode.Attributes)
                            {
                                if (attLast.Name.Equals(att.Name))
                                {
                                    alreadyExist = true;
                                    break;
                                }
                            }

                            if (alreadyExist == false)
                            {
                                objectNode.OwnerDocument.AddAttribute(objectNode, attLast.Name, attLast.Value);
                            }
                        }

                        //copy child
                        foreach (XmlNode node in oldNode.ChildNodes)
                        {
                            var importNode = objectNode.OwnerDocument.ImportNode(node, true);
                            objectNode.AppendChild(importNode);
                        }
                    }
                }
            }
        }

        private XmlElement CreateObjectNode(XmlElement el, ObjectRegistryStatus o)
        {
            var objectNode = el.OwnerDocument.CreateElement("Object");
            el.AppendChild(objectNode);

            el.OwnerDocument.AddAttribute(objectNode, "id", o.Id.ToString());
            el.OwnerDocument.AddAttribute(objectNode, "name", o.Name);
            el.OwnerDocument.AddAttribute(objectNode, "className", o.ClassName);

            return objectNode;
        }

        public XmlElement CreateObjectNode(XmlElement el, Entity ob)
        {
            return CreateObjectNode(el, CreateObjectRegistryStatus(ob, false));
        }

        private void Save(XmlElement el, Entity o)
        {
            o.Save(el, 0);
        }

#endif

        private class ObjectRegistryStatus
        {

#if !EDITOR
			public long PositionInFile;
			public long FileLength;
#endif
            public string ClassName = string.Empty;
            private bool _isLoaded;

            public string Name = string.Empty;
            public int Id = -1;

            private Entity _entity;



            public bool IsLoaded => _isLoaded;

            public Entity Entity
            {
                get => _entity;
                set
                {
                    _entity = value;
                    _isLoaded = _entity != null ? true : false;
                }
            }



            public ObjectRegistryStatus() { }

            public ObjectRegistryStatus(ObjectRegistryStatus o)
            {
#if !EDITOR
				this.FileLength = o_.FileLength;
				this.PositionInFile = o_.PositionInFile;
#endif
                ClassName = o.ClassName;
                _isLoaded = o._isLoaded;
                Name = o.Name;
                Id = o.Id;
                _entity = o._entity;
            }



            public void Close()
            {
                _entity = null;
                _isLoaded = false;
            }

        }



        private readonly List<ObjectRegistryStatus> _objectRegistry = new();

        private int _unusedId = 1; // 0 => unassigned







        public Entity GetObjectById(uint id)
        {
            foreach (var o in _objectRegistry)
            {
                if (o.Id == id)
                {
                    if (o.IsLoaded == false)
                    {
                        Load(o);
                    }

                    if (o.Entity == null)
                    {
                        throw new InvalidOperationException("Entity.GetObjectByID() : Entity is null");
                    }

                    return o.Entity;
                }
            }

            return null;
        }

        public Entity GetObjectByName(string name)
        {
            foreach (var o in _objectRegistry)
            {
                if (o.Name.Equals(name))
                {
                    if (o.IsLoaded == false)
                    {
                        Load(o);
                    }

                    return o.Entity;
                }
            }

            return null;
        }

        public Entity[] GetObjectByType(string className)
        {
            var res = new List<Entity>();

            foreach (var o in _objectRegistry)
            {
                if (className.Equals(o.ClassName))
                {
                    if (o.IsLoaded == false)
                    {
                        Load(o);
                    }

                    res.Add(o.Entity);
                }
            }

            return res.ToArray();
        }

        public bool ContainsObjectId(uint id)
        {
            foreach (var o in _objectRegistry)
            {
                if (o.Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        public bool ContainsObjectNamed(string name)
        {
            foreach (var o in _objectRegistry)
            {
                if (o.Name.Equals(name))
                {
                    return true;
                }
            }

            return false;
        }

        public void Load(XmlElement el, SaveOption option)
        {
#if EDITOR
            Game.Engine.Instance.ObjectRegistry.Clear();
#endif

            var version = uint.Parse(el.Attributes["version"].Value);

            foreach (XmlNode node in el.ChildNodes)
            {
                var o = new ObjectRegistryStatus();

                o.Id = int.Parse(node.Attributes["id"].Value);

                if (_unusedId <= o.Id)
                {
                    _unusedId = o.Id + 1;
                }

                o.Name = node.Attributes["name"].Value;
                o.ClassName = node.Attributes["className"].Value;

                _objectRegistry.Add(o);
            }
        }

        private Entity CreateFromClassName(string className)
        {
            Type t = null;// = Type.GetType(className_);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var a in assemblies)
            {
                t = a.GetType(className, false, true);

                if (t != null)
                {
                    break;
                }
            }

            if (t != null)
            {
                return (Entity)Activator.CreateInstance(t, new object[] { });
            }
            else
            {
                throw new Exception("Can't retrieve the type " + className);
            }
        }


        private void Load(ObjectRegistryStatus objectRegistryStatus)
        {
            if (objectRegistryStatus.IsLoaded)
            {
                return;
            }

            //string projectPath = GameInfo.Instance.GameInfo.Instance.ProjectManager.ProjectPath;			

#if !DEBUG
			try
#endif
            {
                var xmlDoc = new XmlDocument();
                var xmlFile = string.Empty;

#if EDITOR
                xmlFile = Game.Engine.Instance.ProjectManager.ProjectFileOpened;
                xmlDoc.Load(xmlFile);
#else
                CasaEngineGame game = (CasaEngineGame)CasaEngine.Game.Engine.Instance.Game;
                xmlFile = "Content\\" + game.ProjectFile;
                xmlDoc.Load(xmlFile);
#endif
                var xmlPath = ProjectManager.NodeRootName + "/" + ProjectManager.NodeObjectRegistryName + "/" + ProjectManager.NodeObjectName + "[@id='" + objectRegistryStatus.Id + "']";
                var el = (XmlElement)xmlDoc.SelectSingleNode(xmlPath);

                if (el == null)
                {
                    throw new Exception("Can't find the node '" + xmlPath + "' in the xml file " + xmlFile);
                }

                objectRegistryStatus.Entity = CreateFromClassName(objectRegistryStatus.ClassName);
                objectRegistryStatus.Entity.Load(el, SaveOption.Editor);

            }

#if !DEBUG
			catch (System.Exception e)
			{
				throw e;
			}
#endif

            //In Entity Name and ID are temporary fields
#if EDITOR
            //objectRegistryStatus_.Entity.Name = objectRegistryStatus_.Name;
#endif
            objectRegistryStatus.Entity.Id = objectRegistryStatus.Id;
        }

        public void CloseAllObjects()
        {
            foreach (var o in _objectRegistry)
            {
                o.Close();
            }
        }

    }
}
