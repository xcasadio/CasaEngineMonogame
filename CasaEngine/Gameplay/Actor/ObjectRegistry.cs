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
	/// <summary>
	/// 
	/// </summary>
	public
#if EDITOR
	partial
#endif
	class ObjectRegistry
	{
		#region class ObjectRegistryStatus

		/// <summary>
		/// 
		/// </summary>
		private class ObjectRegistryStatus
		{
			#region Fields

#if !EDITOR
			public long PositionInFile;
			public long FileLength;
#endif
			public string ClassName = string.Empty;
			private bool m_IsLoaded = false;
			
			public string Name = string.Empty;
			public int ID = -1;

			private BaseObject m_BaseObject;

			#endregion // Fields

			#region Properties

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

			#endregion // Properties

			#region Constructors

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

			#endregion // Constructors

			#region Methods

			/// <summary>
			/// 
			/// </summary>
			public void Close()
			{
				m_BaseObject = null;
				m_IsLoaded = false;
			}

			#endregion // Methods
		}

		#endregion

		#region Fields

        private List<ObjectRegistryStatus> m_ObjectRegistry = new List<ObjectRegistryStatus>();

		/// <summary>
		/// The unique ID of the entity. It´s used to access the entity when needed.
		/// </summary>
		private int m_UnusedId = 1; // 0 => unassigned

		#endregion // Fields

		#region Properties

		#endregion // Properties

		#region Constructors

		#endregion // Constructors

		#region Methods

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

		#endregion // Methods
	}
}
