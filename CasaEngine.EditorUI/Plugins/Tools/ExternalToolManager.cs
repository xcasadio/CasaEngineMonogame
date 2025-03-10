﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CasaEngine.Core.Log;
using CasaEngine.Engine;
using CasaEngine.Framework.Entities;

namespace CasaEngine.EditorUI.Plugins.Tools;

public class ExternalToolManager
{
    private readonly Dictionary<string, Type> _customObjects = new();
    private readonly Dictionary<string, Type> _customEditorsTemplate = new();
    private readonly Dictionary<Type, IExternalTool> _customEditors = new();
    private readonly Dictionary<string, Assembly> _customObjectAssembly = new();

    public event EventHandler? EventExternalToolChanged;

    public void Initialize(string directory)
    {
        Clear();

        if (string.IsNullOrEmpty(EngineEnvironment.ProjectPath))
        {
            return;
        }

        var fullPath = Path.Combine(EngineEnvironment.ProjectPath, directory);
        var msg = string.Empty;

        if (!Directory.Exists(fullPath))
        {
            Logs.WriteError($"ExternalToolManager : can't find the directory {fullPath}");
            return;
        }

        foreach (var file in Directory.GetFiles(fullPath, "*.dll"))
        {
            var assembly = AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(file));

            try
            {
                foreach (var t in assembly.GetTypes())
                {
                    foreach (var intf in t.GetInterfaces())
                    {
                        //if (intf.Equals(typeof(IContentObject)))
                        //{
                        //    _customObjects.Add(t.ToString(), t);
                        //    break;
                        //}
                    }

                    foreach (var attribute in t.GetCustomAttributes(true).Where(x => x is CustomEditor))
                    {
                        if (t.GetInterfaces().Any(inter => inter == typeof(IExternalTool)))
                        {
                            RegisterEditor(attribute.ToString(), t);
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException rtle)
            {
                msg = rtle.LoaderExceptions.Aggregate(msg, (current, e) => current + e.Message + Environment.NewLine);
                throw new Exception(msg);
            }
        }

        EventExternalToolChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RegisterEditor(string objectTypeName, Type editorType)
    {
        _customEditorsTemplate.Add(objectTypeName, editorType);
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

    public Entity CreateCustomObjectByName(string name)
    {
        return (Entity)_customObjects[name].Assembly.CreateInstance(_customObjects[name].FullName);
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

    public void RunSubEditor(string path, Entity obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException("ExternalToolManager.RunSubEditor() : Entity is null");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException("ExternalToolManager.RunSubEditor() : path_ is null or empty");
        }

        if (_customEditorsTemplate.ContainsKey(obj.GetType().FullName))
        {
            var t = _customEditorsTemplate[obj.GetType().FullName];
            IExternalTool tool = null;

            if (_customEditors.ContainsKey(t))
            {
                tool = _customEditors[t];
            }

            if (tool == null
                || tool.ExternalTool.Window.IsActive)
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
                    //DebugHelper.ShowException(ex);
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
            Logs.WriteWarning("Can't find editor for type of object '" + obj.GetType().FullName + "'");
        }
    }

    private void CloseAllSubEditor()
    {
        foreach (var p in _customEditors.Where(p => p.Value.ExternalTool.Window is { IsActive: true }))
        {
            p.Value.ExternalTool.Window.Close();
        }
    }
}