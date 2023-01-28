using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.Game;
using CasaEngineCommon.Extension;
using System.Reflection;
using CasaEngine;
using CasaEngineCommon.Design;
using CasaEngine.Project;

namespace CasaEngine.Gameplay.Actor
{
    public class ObjectRegistry
    {
#if EDITOR
        static private readonly uint m_Version = 1;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        /// <param name="add_"></param>
        /// <returns></returns>
        private ObjectRegistryStatus CreateObjectRegistryStatus(BaseObject ob_, bool add_)
        {
            ObjectRegistryStatus objectRegistryStatus = new ObjectRegistryStatus();
            objectRegistryStatus.ClassName = ob_.GetType().FullName;
            objectRegistryStatus.BaseObject = ob_;
            //objectRegistryStatus.Name = ob_.Name;

            if (add_ == true)
            {
                objectRegistryStatus.ID = m_UnusedId++;
                ob_.ID = objectRegistryStatus.ID;
            }
            else
            {
                objectRegistryStatus.ID = ob_.ID;
            }

            return objectRegistryStatus;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj_"></param>
        /// <param name="name_"></param>
        public void AddObject(BaseObject obj_, string name_)
        {
            //obj_.Name = name_;
            ObjectRegistryStatus o = CreateObjectRegistryStatus(obj_, true);
            m_ObjectRegistry.Add(o);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj_"></param>
        public void AddObject(BaseObject obj_)
        {
            //AddObject(obj_, obj_.Name);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        public void RemoveObject(string name_)
        {
            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                if (o.Name.Equals(name_) == true)
                {
                    m_ObjectRegistry.Remove(o);
                    break;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <param name="newName_"></param>
        /// <returns></returns>
        public void RenameObject(string name_, string newName_)
        {
            if (ContainsObjectNamed(newName_) == true)
            {
                throw new Exception("m_ObjectRegistry.RenameObject() : an object named " + newName_ + " already exist.");
            }

            if (ContainsObjectNamed(name_) == false)
            {
                throw new Exception("m_ObjectRegistry.RenameObject() : the object named " + name_ + " doesn't exist.");
            }

            for (int i = 0; i < m_ObjectRegistry.Count; i++)
            {
                if (m_ObjectRegistry[i].Name.Equals(name_) == true)
                {
                    ObjectRegistryStatus o = new ObjectRegistryStatus(m_ObjectRegistry[i]);
                    o.Name = newName_;

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

                    m_ObjectRegistry[i] = o;
                    break;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            m_ObjectRegistry.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<string> GetAllObjectName()
        {
            List<string> res = new List<string>();

            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                res.Add(o.Name);
            }

            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <returns></returns>
        public bool IsValidName(string name_)
        {
            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                if (o.Name.Equals(name_) == true)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <returns></returns>
        public int GetObjectIDByName(string name_)
        {
            if (string.IsNullOrEmpty(name_))
            {
                throw new ArgumentNullException("Please give a valid no empty string");
            }

            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                if (o.Name.Equals(name_) == true)
                {
                    return o.ID;
                }
            }

            throw new Exception("The object named " + name_ + " doesn't exist");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Save(XmlElement el_, SaveOption option_)
        {
            el_.OwnerDocument.AddAttribute(el_, "version", m_Version.ToString());

            XmlDocument xmlDocLast = null;
            if (string.IsNullOrEmpty(Engine.Instance.ProjectManager.ProjectFileOpened) == false
                && File.Exists(Engine.Instance.ProjectManager.ProjectFileOpened))
            {
                xmlDocLast = new XmlDocument();
                xmlDocLast.Load(Engine.Instance.ProjectManager.ProjectFileOpened);
            }

            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                XmlElement objectNode = CreateObjectNode(el_, o);

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
                    XmlNode oldNode = xmlDocLast.SelectSingleNode("Project/ObjectRegistry/Object[@id='" + o.ID + "']");

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <returns></returns>
        private XmlElement CreateObjectNode(XmlElement el_, ObjectRegistryStatus o)
        {
            XmlElement objectNode = el_.OwnerDocument.CreateElement("Object");
            el_.AppendChild(objectNode);

            el_.OwnerDocument.AddAttribute(objectNode, "id", o.ID.ToString());
            el_.OwnerDocument.AddAttribute(objectNode, "name", o.Name);
            el_.OwnerDocument.AddAttribute(objectNode, "className", o.ClassName);

            return objectNode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="ob_"></param>
        /// <returns></returns>
        public XmlElement CreateObjectNode(XmlElement el_, BaseObject ob_)
        {
            return CreateObjectNode(el_, CreateObjectRegistryStatus(ob_, false));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="o_"></param>
        private void Save(XmlElement el_, BaseObject o_)
        {
            o_.Save(el_, 0);
        }

#endif

        /// <summary>
        /// 
        /// </summary>
        private class ObjectRegistryStatus
        {

#if !EDITOR
			public long PositionInFile;
			public long FileLength;
#endif
            public string ClassName = string.Empty;
            private bool m_IsLoaded = false;

            public string Name = string.Empty;
            public int ID = -1;

            private BaseObject m_BaseObject;



            /// <summary>
            /// Gets
            /// </summary>
            public bool IsLoaded
            {
                get
                {
                    return m_IsLoaded;
                }
            }

            /// <summary>
            /// Gets/Sets
            /// </summary>
            public BaseObject BaseObject
            {
                get { return m_BaseObject; }
                set
                {
                    m_BaseObject = value;
                    m_IsLoaded = m_BaseObject != null ? true : false;
                }
            }



            /// <summary>
            /// 
            /// </summary>
            public ObjectRegistryStatus() { }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="o_"></param>
            public ObjectRegistryStatus(ObjectRegistryStatus o_)
            {
#if !EDITOR
				this.FileLength = o_.FileLength;
				this.PositionInFile = o_.PositionInFile;
#endif
                this.ClassName = o_.ClassName;
                this.m_IsLoaded = o_.m_IsLoaded;
                this.Name = o_.Name;
                this.ID = o_.ID;
                this.m_BaseObject = o_.m_BaseObject;
            }



            /// <summary>
            /// 
            /// </summary>
            public void Close()
            {
                m_BaseObject = null;
                m_IsLoaded = false;
            }

        }



        private List<ObjectRegistryStatus> m_ObjectRegistry = new List<ObjectRegistryStatus>();

        /// <summary>
        /// The unique ID of the entity. It´s used to access the entity when needed.
        /// </summary>
        private int m_UnusedId = 1; // 0 => unassigned







        /// <summary>
        /// 
        /// </summary>
        /// <param name="id_"></param>
        /// <returns></returns>
        public BaseObject GetObjectByID(uint id_)
        {
            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                if (o.ID == id_)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <returns></returns>
        public BaseObject GetObjectByName(string name_)
        {
            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                if (o.Name.Equals(name_) == true)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="className_"></param>
        /// <returns></returns>
        public BaseObject[] GetObjectByType(string className_)
        {
            List<BaseObject> res = new List<BaseObject>();

            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                if (className_.Equals(o.ClassName))
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id_"></param>
        /// <returns></returns>
        public bool ContainsObjectID(uint id_)
        {
            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                if (o.ID == id_)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <returns></returns>
        public bool ContainsObjectNamed(string name_)
        {
            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                if (o.Name.Equals(name_))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Load(XmlElement el_, SaveOption option_)
        {
#if EDITOR
            Engine.Instance.ObjectRegistry.Clear();
#endif

            uint version = uint.Parse(el_.Attributes["version"].Value);

            foreach (XmlNode node in el_.ChildNodes)
            {
                ObjectRegistryStatus o = new ObjectRegistryStatus();

                o.ID = int.Parse(node.Attributes["id"].Value);

                if (m_UnusedId <= o.ID)
                {
                    m_UnusedId = o.ID + 1;
                }

                o.Name = node.Attributes["name"].Value;
                o.ClassName = node.Attributes["className"].Value;

                m_ObjectRegistry.Add(o);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        /// <param name="className_"></param>
        /// <param name="externalData_"></param>
        /// <returns></returns>
        private BaseObject CreateFromClassName(string className_)
        {
            Type t = null;// = Type.GetType(className_);

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly a in assemblies)
            {
                t = a.GetType(className_, false, true);

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
                throw new Exception("Can't retrieve the type " + className_);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        private void Load(ObjectRegistryStatus objectRegistryStatus_)
        {
            if (objectRegistryStatus_.IsLoaded == true)
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
                string xmlPath = ProjectManager.NodeRootName + "/" + ProjectManager.NodeObjectRegistryName + "/" + ProjectManager.NodeObjectName + "[@id='" + objectRegistryStatus_.ID + "']";
                XmlElement el = (XmlElement)xmlDoc.SelectSingleNode(xmlPath);

                if (el == null)
                {
                    throw new Exception("Can't find the node '" + xmlPath + "' in the xml file " + xmlFile);
                }

                objectRegistryStatus_.BaseObject = CreateFromClassName(objectRegistryStatus_.ClassName);
                objectRegistryStatus_.BaseObject.Load(el, SaveOption.Editor);

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
            objectRegistryStatus_.BaseObject.ID = objectRegistryStatus_.ID;
        }

        /// <summary>
        /// 
        /// </summary>
        public void CloseAllObjects()
        {
            foreach (ObjectRegistryStatus o in m_ObjectRegistry)
            {
                o.Close();
            }
        }

    }
}
