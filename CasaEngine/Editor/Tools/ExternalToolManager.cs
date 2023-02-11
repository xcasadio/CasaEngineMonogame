using System.Reflection;
using CasaEngine.Game;
using CasaEngine.Project;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngineCommon.Logger;

namespace CasaEngine.Editor.Tools
{
    public class ExternalToolManager
    {
        readonly Dictionary<string, Type> _customObjects = new();
        readonly Dictionary<string, Type> _customEditorsTemplate = new();
        readonly Dictionary<Type, IExternalTool> _customEditors = new();
        readonly Dictionary<string, Assembly> _customObjectAssembly = new();

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
                                _customObjects.Add(t.ToString(), t);
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
                catch (ReflectionTypeLoadException rtle)
                {
                    foreach (Exception e in rtle.LoaderExceptions)
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

        public void RegisterEditor(string objectTypeName, Type editorType)
        {
            _customEditorsTemplate.Add(objectTypeName, editorType);
        }

        public string[] GetAllCustomObjectNames()
        {
            List<string> res = new List<string>();

            foreach (var pair in _customObjects)
            {
                res.Add(pair.Key);
            }

            return res.ToArray();
        }

        /*public string[] GetAllToolNames()
		{
			List<string> res = new List<string>();

            foreach (KeyValuePair<string, IExternalTool> pair in _Tools)
			{
				res.Add(pair.Key);
			}

			return res.ToArray();
		}*/

        public BaseObject CreateCustomObjectByName(string name)
        {
            return (BaseObject)_customObjects[name].Assembly.CreateInstance(_customObjects[name].FullName);
        }

        /*public void RunTool(System.Windows.Forms.Form parent, string name_)
		{
            if (_Tools.ContainsKey(name_) == true)
            {
                _Tools[name_].Run(parent);
            }
		}

		public void CloseAllTool()
		{
            foreach (KeyValuePair<string, IExternalTool> pair in _Tools)
			{
				pair.Value.Close();
			}
		}*/

        public void Clear()
        {
            CloseAllSubEditor();
            _customEditors.Clear();
            _customEditorsTemplate.Clear();
            _customObjects.Clear();
            _customObjectAssembly.Clear();
        }


        public void RunSubEditor(string path, BaseObject obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("ExternalToolManager.RunSubEditor() : BaseObject is null");
            }

            if (string.IsNullOrWhiteSpace(path) == true)
            {
                throw new ArgumentNullException("ExternalToolManager.RunSubEditor() : path_ is null or empty");
            }

            if (_customEditorsTemplate.ContainsKey(obj.GetType().FullName) == true)
            {
                Type t = _customEditorsTemplate[obj.GetType().FullName];
                IExternalTool tool = null;

                if (_customEditors.ContainsKey(t) == true)
                {
                    tool = _customEditors[t];
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

                    _customEditors.Remove(t);
                    _customEditors.Add(t, tool);
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

                tool.SetCurrentObject(path, obj);
            }
            else
            {
                LogManager.Instance.WriteLine("Can't find editor for type of object '" + obj.GetType().FullName + "'");
            }
        }

        private void CloseAllSubEditor()
        {
            foreach (var p in _customEditors)
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
