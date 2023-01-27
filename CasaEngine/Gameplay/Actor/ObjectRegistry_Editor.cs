using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.Game;
using CasaEngineCommon.Extension;
using CasaEngine;
using CasaEngineCommon.Design;

namespace CasaEngine.Gameplay.Actor
{
	/// <summary>
	/// 
	/// </summary>
	public partial class ObjectRegistry
	{
		#region Fields

        static private readonly uint m_Version = 1;

		#endregion // Fields

		#region Properties

		#endregion // Properties

		#region Constructors

		#endregion // Constructors

		#region Methods

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

		#endregion // Methods
	}
}
