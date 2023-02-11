using System.Xml;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.Game;
using CasaEngineCommon.Extension;
using System.Reflection;
using CasaEngineCommon.Design;
using CasaEngine.Project;

namespace CasaEngine.Gameplay.Actor
{
    public class ObjectRegistry
    {
#if EDITOR
        static private readonly uint Version = 1;

        private ObjectRegistryStatus CreateObjectRegistryStatus(BaseObject ob, bool add)
        {
            ObjectRegistryStatus objectRegistryStatus = new ObjectRegistryStatus();
            objectRegistryStatus.ClassName = ob.GetType().FullName;
            objectRegistryStatus.BaseObject = ob;
            //objectRegistryStatus.Name = ob_.Name;

            if (add == true)
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

        public void AddObject(BaseObject obj, string name)
        {
            //obj_.Name = name_;
            ObjectRegistryStatus o = CreateObjectRegistryStatus(obj, true);
            _objectRegistry.Add(o);
        }

        public void AddObject(BaseObject obj)
        {
            //AddObject(obj_, obj_.Name);
        }

        public void RemoveObject(string name)
        {
            foreach (ObjectRegistryStatus o in _objectRegistry)
            {
                if (o.Name.Equals(name) == true)
                {
                    _objectRegistry.Remove(o);
                    break;
                }
            }
        }

        public void RenameObject(string name, string newName)
        {
            if (ContainsObjectNamed(newName) == true)
            {
                throw new Exception("_ObjectRegistry.RenameObject() : an object named " + newName + " already exist.");
            }

            if (ContainsObjectNamed(name) == false)
            {
                throw new Exception("_ObjectRegistry.RenameObject() : the object named " + name + " doesn't exist.");
            }

            for (int i = 0; i < _objectRegistry.Count; i++)
            {
                if (_objectRegistry[i].Name.Equals(name) == true)
                {
                    ObjectRegistryStatus o = new ObjectRegistryStatus(_objectRegistry[i]);
                    o.Name = newName;

                    bool loaded = o.IsLoaded;
                    if (o.IsLoaded == false)
                    {
                        Load(o);
                    }
                    //o.BaseObject.Name = newName_;
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
            List<string> res = new List<string>();

            foreach (ObjectRegistryStatus o in _objectRegistry)
            {
                res.Add(o.Name);
            }

            return res;
        }

        public bool IsValidName(string name)
        {
            foreach (ObjectRegistryStatus o in _objectRegistry)
            {
                if (o.Name.Equals(name) == true)
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

            foreach (ObjectRegistryStatus o in _objectRegistry)
            {
                if (o.Name.Equals(name) == true)
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
            if (string.IsNullOrEmpty(Engine.Instance.ProjectManager.ProjectFileOpened) == false
                && File.Exists(Engine.Instance.ProjectManager.ProjectFileOpened))
            {
                xmlDocLast = new XmlDocument();
                xmlDocLast.Load(Engine.Instance.ProjectManager.ProjectFileOpened);
            }

            foreach (ObjectRegistryStatus o in _objectRegistry)
            {
                XmlElement objectNode = CreateObjectNode(el, o);

                if (o.IsLoaded == true)
                {
                    o.BaseObject.Save(objectNode, SaveOption.Editor);
                }

                //Si on sauvegarde et qu'il y a deja une sauvegarde
                //On est obligé de mixer le nouveau fichier avec le nouveau
                //Car on ne sauvegarde a chaque fois que le monde courant avec ses objets chargés
                //(on ne peut pas sauvegarder les autres mondes sinon il faudrait les chargés a chaque fois)
                else if (xmlDocLast != null)
                {
                    XmlNode oldNode = xmlDocLast.SelectSingleNode("Project/ObjectRegistry/Object[@id='" + o.Id + "']");

                    if (oldNode != null)
                    {
                        //copy attributes
                        bool alreadyExist = false;
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
                            XmlNode importNode = objectNode.OwnerDocument.ImportNode(node, true);
                            objectNode.AppendChild(importNode);
                        }
                    }
                }
            }
        }

        private XmlElement CreateObjectNode(XmlElement el, ObjectRegistryStatus o)
        {
            XmlElement objectNode = el.OwnerDocument.CreateElement("Object");
            el.AppendChild(objectNode);

            el.OwnerDocument.AddAttribute(objectNode, "id", o.Id.ToString());
            el.OwnerDocument.AddAttribute(objectNode, "name", o.Name);
            el.OwnerDocument.AddAttribute(objectNode, "className", o.ClassName);

            return objectNode;
        }

        public XmlElement CreateObjectNode(XmlElement el, BaseObject ob)
        {
            return CreateObjectNode(el, CreateObjectRegistryStatus(ob, false));
        }

        private void Save(XmlElement el, BaseObject o)
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
            private bool _isLoaded = false;

            public string Name = string.Empty;
            public int Id = -1;

            private BaseObject _baseObject;



            public bool IsLoaded => _isLoaded;

            public BaseObject BaseObject
            {
                get => _baseObject;
                set
                {
                    _baseObject = value;
                    _isLoaded = _baseObject != null ? true : false;
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
                _baseObject = o._baseObject;
            }



            public void Close()
            {
                _baseObject = null;
                _isLoaded = false;
            }

        }



        private readonly List<ObjectRegistryStatus> _objectRegistry = new();

        private int _unusedId = 1; // 0 => unassigned







        public BaseObject GetObjectById(uint id)
        {
            foreach (ObjectRegistryStatus o in _objectRegistry)
            {
                if (o.Id == id)
                {
                    if (o.IsLoaded == false)
                    {
                        Load(o);
                    }

                    if (o.BaseObject == null)
                    {
                        throw new InvalidOperationException("BaseObject.GetObjectByID() : BaseObject is null");
                    }

                    return o.BaseObject;
                }
            }

            return null;
        }

        public BaseObject GetObjectByName(string name)
        {
            foreach (ObjectRegistryStatus o in _objectRegistry)
            {
                if (o.Name.Equals(name) == true)
                {
                    if (o.IsLoaded == false)
                    {
                        Load(o);
                    }

                    return o.BaseObject;
                }
            }

            return null;
        }

        public BaseObject[] GetObjectByType(string className)
        {
            List<BaseObject> res = new List<BaseObject>();

            foreach (ObjectRegistryStatus o in _objectRegistry)
            {
                if (className.Equals(o.ClassName))
                {
                    if (o.IsLoaded == false)
                    {
                        Load(o);
                    }

                    res.Add(o.BaseObject);
                }
            }

            return res.ToArray();
        }

        public bool ContainsObjectId(uint id)
        {
            foreach (ObjectRegistryStatus o in _objectRegistry)
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
            foreach (ObjectRegistryStatus o in _objectRegistry)
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
            Engine.Instance.ObjectRegistry.Clear();
#endif

            uint version = uint.Parse(el.Attributes["version"].Value);

            foreach (XmlNode node in el.ChildNodes)
            {
                ObjectRegistryStatus o = new ObjectRegistryStatus();

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

        private BaseObject CreateFromClassName(string className)
        {
            Type t = null;// = Type.GetType(className_);

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly a in assemblies)
            {
                t = a.GetType(className, false, true);

                if (t != null)
                {
                    break;
                }
            }

            if (t != null)
            {
                return (BaseObject)Activator.CreateInstance(t, new object[] { });
            }
            else
            {
                throw new Exception("Can't retrieve the type " + className);
            }
        }


        private void Load(ObjectRegistryStatus objectRegistryStatus)
        {
            if (objectRegistryStatus.IsLoaded == true)
            {
                return;
            }

            //string projectPath = GameInfo.Instance.GameInfo.Instance.ProjectManager.ProjectPath;			

#if !DEBUG
			try
#endif
            {
                XmlDocument xmlDoc = new XmlDocument();
                string xmlFile = string.Empty;

#if EDITOR
                xmlFile = Engine.Instance.ProjectManager.ProjectFileOpened;
                xmlDoc.Load(xmlFile);
#else
                CasaEngineGame game = (CasaEngineGame)Engine.Instance.Game;
                xmlFile = "Content\\" + game.ProjectFile;
                xmlDoc.Load(xmlFile);
#endif
                string xmlPath = ProjectManager.NodeRootName + "/" + ProjectManager.NodeObjectRegistryName + "/" + ProjectManager.NodeObjectName + "[@id='" + objectRegistryStatus.Id + "']";
                XmlElement el = (XmlElement)xmlDoc.SelectSingleNode(xmlPath);

                if (el == null)
                {
                    throw new Exception("Can't find the node '" + xmlPath + "' in the xml file " + xmlFile);
                }

                objectRegistryStatus.BaseObject = CreateFromClassName(objectRegistryStatus.ClassName);
                objectRegistryStatus.BaseObject.Load(el, SaveOption.Editor);

            }

#if !DEBUG
			catch (System.Exception e)
			{
				throw e;
			}
#endif

            //In BaseObject Name and ID are temporary fields
#if EDITOR
            //objectRegistryStatus_.BaseObject.Name = objectRegistryStatus_.Name;
#endif
            objectRegistryStatus.BaseObject.Id = objectRegistryStatus.Id;
        }

        public void CloseAllObjects()
        {
            foreach (ObjectRegistryStatus o in _objectRegistry)
            {
                o.Close();
            }
        }

    }
}
