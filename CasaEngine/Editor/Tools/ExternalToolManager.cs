using System.Reflection;
using CasaEngine.Game;
using CasaEngine.Project;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngineCommon.Logger;

namespace CasaEngine.Editor.Tools
{
    public class ExternalToolManager
    {
        readonly Dictionary<string, Type> m_CustomObjects = new Dictionary<string, Type>();
        readonly Dictionary<string, Type> m_CustomEditorsTemplate = new Dictionary<string, Type>();
        readonly Dictionary<Type, IExternalTool> m_CustomEditors = new Dictionary<Type, IExternalTool>();
        readonly Dictionary<string, Assembly> m_CustomObjectAssembly = new Dictionary<string, Assembly>();

        public event EventHandler EventExternalToolChanged;







        public void Initialize()
        {
            Clear();

            if (string.IsNullOrEmpty(Engine.Instance.ProjectManager.ProjectPath) == true)
            {
                return;
            }

            string fullPath = Engine.Instance.ProjectManager.ProjectPath;
            fullPath += Path.DirectorySeparatorChar + ProjectManager.ExternalToolsDirPath;

            //AppDomain.CurrentDomain.SetupInformation.PrivateBiAnPath = fullPath;

            Assembly assembly;
            string msg = string.Empty;

            foreach (string file in Directory.GetFiles(fullPath, "*.dll"))
            {
                assembly = AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(file));

                try
                {
                    foreach (Type t in assembly.GetTypes())
                    {
                        foreach (Type intf in t.GetInterfaces())
                        {
                            if (intf.Equals(typeof(IContentObject)))
                            {
                                m_CustomObjects.Add(t.ToString(), t);
                                break;
                            }
                        }

                        foreach (object attribute in t.GetCustomAttributes(true))
                        {
                            if (attribute is CustomEditor)
                            {
                                foreach (Type inter in t.GetInterfaces())
                                {
                                    if (inter.Equals(typeof(IExternalTool)) == true)
                                    {
                                        RegisterEditor(attribute.ToString(), t);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException RTLE)
                {
                    foreach (Exception e in RTLE.LoaderExceptions)
                    {
                        msg += e.Message + "\n";
                    }

                    throw new Exception(msg);
                }
            }

            if (EventExternalToolChanged != null)
            {
                EventExternalToolChanged.Invoke(this, EventArgs.Empty);
            }
        }

        public void RegisterEditor(string objectTypeName_, Type editorType_)
        {
            m_CustomEditorsTemplate.Add(objectTypeName_, editorType_);
        }

        public string[] GetAllCustomObjectNames()
        {
            List<string> res = new List<string>();

            foreach (var pair in m_CustomObjects)
            {
                res.Add(pair.Key);
            }

            return res.ToArray();
        }

        /*public string[] GetAllToolNames()
		{
			List<string> res = new List<string>();

            foreach (KeyValuePair<string, IExternalTool> pair in m_Tools)
			{
				res.Add(pair.Key);
			}

			return res.ToArray();
		}*/

        public BaseObject CreateCustomObjectByName(string name_)
        {
            return (BaseObject)m_CustomObjects[name_].Assembly.CreateInstance(m_CustomObjects[name_].FullName);
        }

        /*public void RunTool(System.Windows.Forms.Form parent, string name_)
		{
            if (m_Tools.ContainsKey(name_) == true)
            {
                m_Tools[name_].Run(parent);
            }
		}

		public void CloseAllTool()
		{
            foreach (KeyValuePair<string, IExternalTool> pair in m_Tools)
			{
				pair.Value.Close();
			}
		}*/

        public void Clear()
        {
            CloseAllSubEditor();
            m_CustomEditors.Clear();
            m_CustomEditorsTemplate.Clear();
            m_CustomObjects.Clear();
            m_CustomObjectAssembly.Clear();
        }


        public void RunSubEditor(string path_, BaseObject obj_)
        {
            if (obj_ == null)
            {
                throw new ArgumentNullException("ExternalToolManager.RunSubEditor() : BaseObject is null");
            }

            if (string.IsNullOrWhiteSpace(path_) == true)
            {
                throw new ArgumentNullException("ExternalToolManager.RunSubEditor() : path_ is null or empty");
            }

            if (m_CustomEditorsTemplate.ContainsKey(obj_.GetType().FullName) == true)
            {
                Type t = m_CustomEditorsTemplate[obj_.GetType().FullName];
                IExternalTool tool = null;

                if (m_CustomEditors.ContainsKey(t) == true)
                {
                    tool = m_CustomEditors[t];
                }

                if (tool == null
                    || tool.ExternalTool.Window.IsDisposed == true)
                {
#if !DEBUG
                    try
                    {
#endif
                    tool = (IExternalTool)t.Assembly.CreateInstance(t.FullName);
                    tool.ExternalTool.Window.Show();

                    m_CustomEditors.Remove(t);
                    m_CustomEditors.Add(t, tool);
#if !DEBUG
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.ShowException(ex);
                    }
#endif                    
                }
                else
                {
                    tool.ExternalTool.Window.Focus();
                }

                tool.SetCurrentObject(path_, obj_);
            }
            else
            {
                LogManager.Instance.WriteLine("Can't find editor for type of object '" + obj_.GetType().FullName + "'");
            }
        }

        private void CloseAllSubEditor()
        {
            foreach (var p in m_CustomEditors)
            {
                if (p.Value.ExternalTool.Window != null
                    && p.Value.ExternalTool.Window.IsDisposed == false)
                {
                    p.Value.ExternalTool.Window.Close();
                }
            }
        }


    }
}
