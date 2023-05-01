using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using CasaEngine.Core.Logger;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls;

public class EditorControlHelper
{
    public static void ShowControl(UserControl control, DockingManager dockingManager, string panelTitle)
    {
        if (FindControlLayoutPane(control, dockingManager.Layout.Children))
        {
            return;
        }

        var parent = control.FindParent<DockingManager>();
        if (parent != null)
        {
            RemoveControlFromLayoutPane(control, parent.Layout);
            //control.Parent is not null after this call.
            //The workaround is to call RemoveLogicalChild
            var methodInfo = parent.GetType().GetMethod("RemoveLogicalChild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            methodInfo.Invoke(parent, new object?[] { control });
        }

        dockingManager.Dispatcher.Invoke(() =>
        {
            var layoutAnchorable = new LayoutAnchorable { Content = control, Title = panelTitle };
            dockingManager.Layout.BottomSide = new LayoutAnchorSide { Children = { new LayoutAnchorGroup { Children = { layoutAnchorable } } } };
            layoutAnchorable.Show();
            dockingManager.UpdateLayout();
        });
    }

    private static bool RemoveControlFromLayoutPane(UserControl control, ILayoutContainer layoutContainerParent)
    {
        foreach (var layoutElement in layoutContainerParent.Children)
        {
            if (layoutElement is LayoutContent layoutContent && Equals(layoutContent.Content, control))
            {
                layoutContent.Close();
                layoutContent.Content = null;
                //layoutContainerParent.RemoveChild(layoutContent);
                return true;
            }

            if (layoutElement is ILayoutContainer layoutContainer)
            {
                if (RemoveControlFromLayoutPane(control, layoutContainer))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool FindControlLayoutPane(UserControl control, IEnumerable<ILayoutElement> layoutChildren)
    {
        bool found = false;
        foreach (var layoutElement in layoutChildren)
        {
            if (layoutElement is LayoutContent layoutContent && Equals(layoutContent.Content, control))
            {
                layoutContent.IsEnabled = true;
                layoutContent.IsActive = true;
                layoutContent.IsSelected = true;

                if (layoutContent is LayoutAnchorable layoutAnchorable)
                {
                    layoutAnchorable.Show();
                }

                return true;
            }

            if (layoutElement is ILayoutContainer layoutContainer)
            {
                found |= FindControlLayoutPane(control, layoutContainer.Children);
            }
        }

        return found;
    }

    public static void LoadLayout(DockingManager dockingManager, string fileName, EventHandler<LayoutSerializationCallbackEventArgs> layoutSerializationCallback)
    {
        try
        {
            if (!File.Exists(fileName))
            {
                return;
            }

            XmlLayoutSerializer layoutSerializer = new XmlLayoutSerializer(dockingManager);
            layoutSerializer.LayoutSerializationCallback += layoutSerializationCallback;
            using var reader = new StreamReader(fileName);
            layoutSerializer.Deserialize(reader);
            LogManager.Instance.WriteLineDebug($"Load Layout '{fileName}'");
        }
        catch (Exception e)
        {
            LogManager.Instance.WriteException(e);
        }
    }

    public static void SaveLayout(DockingManager dockingManager, string fileName)
    {
        try
        {
            XmlLayoutSerializer layoutSerializer = new XmlLayoutSerializer(dockingManager);
            using var writer = new StreamWriter(fileName);
            layoutSerializer.Serialize(writer);
            LogManager.Instance.WriteLineDebug($"Save Layout '{fileName}'");

        }
        catch (Exception e)
        {
            LogManager.Instance.WriteException(e);
        }
    }
}