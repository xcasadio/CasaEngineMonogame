using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Editor.Controls.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// What the automation (<c>--cutscene-edit-ops</c>, ADR-0140 P14) needs from the tree: the menu of the selection opened at its
/// row and a drag in progress put in a given state without a mouse. Not used by the editor itself.
/// </summary>
internal sealed partial class CutsceneOutlinerPanel
{
    /// <summary>The outcome of an automation request on the tree.</summary>
    internal enum AutomationOutcome
    {
        /// <summary>The request was carried out.</summary>
        Done,

        /// <summary>The tree has no layout yet: ask again on a later frame.</summary>
        NotReady,

        /// <summary>The request cannot be carried out; the message says why.</summary>
        Refused,
    }

    /// <summary>
    /// Opens the context menu of the selected action at the position of its row (the end of the tree without a selection), the
    /// way a right click does. The automatic closing of the menu when the mouse is far away is turned off: the real mouse is not
    /// where the menu is.
    /// </summary>
    internal AutomationOutcome TryOpenMenuForAutomation(out string message)
    {
        message = string.Empty;
        var treeView = _treeView;
        if (treeView?.ScrollViewer == null || _document?.Session?.Asset.RootAction == null)
        {
            message = "no cutscene is shown in the tree";
            return AutomationOutcome.Refused;
        }

        var selected = _document.SelectedAction;
        var anchor = Point.Zero;
        if (selected != null && _actionToItem.TryGetValue(selected, out var item))
        {
            if (!TryGetRowScreenBounds(item, out var row))
            {
                return AutomationOutcome.NotReady;
            }

            anchor = new Point(row.Left + 24, row.Bottom);
        }
        else
        {
            var viewport = treeView.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, treeView.ScrollViewer.ContentViewport);
            if (viewport.Width <= 0 || viewport.Height <= 0)
            {
                return AutomationOutcome.NotReady;
            }

            anchor = new Point(viewport.Left + 24, viewport.Top + 24);
        }

        var menu = CutsceneActionMenu.Create(_window, _document, ShowRefusal);
        if (menu == null)
        {
            message = "no operation is allowed on the selection, so there is no menu";
            return AutomationOutcome.Refused;
        }

        menu.AutoCloseThreshold = null;
        if (!treeView.GetDesktop().TryOpenContextMenu(menu, anchor))
        {
            message = "the menu did not open";
            return AutomationOutcome.Refused;
        }

        message = $"opened at {anchor.X},{anchor.Y}";
        return AutomationOutcome.Done;
    }

    /// <summary>
    /// Puts the tree in the state of a drag of <paramref name="dragged"/> whose pointer is in the zone <paramref name="kind"/> of
    /// the row of <paramref name="target"/>, without a mouse. The state stays until the tree is rebuilt or the drag is cancelled,
    /// so the marker is drawn for a capture. <paramref name="message"/> tells what the destination is.
    /// </summary>
    internal AutomationOutcome TrySetDragHoverForAutomation(CutsceneActionData dragged, CutsceneActionData target, CutsceneDropKind kind, out string message)
    {
        message = string.Empty;
        var root = _document?.Session?.Asset.RootAction;
        if (_treeView == null || root == null)
        {
            message = "no cutscene is shown in the tree";
            return AutomationOutcome.Refused;
        }

        if (kind is not (CutsceneDropKind.Before or CutsceneDropKind.After or CutsceneDropKind.Into))
        {
            message = "the zone is not before, after or into";
            return AutomationOutcome.Refused;
        }

        CancelDrag();
        if (!TryFillDropRows(out var area))
        {
            return AutomationOutcome.NotReady;
        }

        int rowIndex = -1;
        for (int index = 0; index < _visibleRows.Count; index++)
        {
            if (ReferenceEquals(_visibleRows[index].Action, target))
            {
                rowIndex = index;
                break;
            }
        }

        if (rowIndex < 0)
        {
            message = "the target row is not shown (a parent is collapsed)";
            return AutomationOutcome.Refused;
        }

        bool isContainer = CutsceneActionCatalog.CanHaveChildren(target);
        if (kind == CutsceneDropKind.Into && !isContainer)
        {
            message = "the target is not a container, so it has no into zone";
            return AutomationOutcome.Refused;
        }

        var row = _dropRows[rowIndex];
        float bandBottom = rowIndex + 1 < _dropRows.Count ? _dropRows[rowIndex + 1].Top : row.Top + row.Height;
        float band = bandBottom - row.Top;

        // The middle of a zone of the band that the resolver reads: a container has quarters and a half, a leaf has two halves.
        float fraction = kind switch
        {
            CutsceneDropKind.Before => isContainer ? 0.125f : 0.25f,
            CutsceneDropKind.Into => 0.5f,
            _ => isContainer ? 0.875f : 0.75f,
        };

        float x = System.Math.Clamp(_rowLefts[rowIndex] + 8f, area.Left, area.Right - 1f);
        _dragAction = dragged;
        _dragPointer = new Vector2(x, row.Top + (band * fraction));
        var result = RefreshDragTarget();
        if (result.Kind == CutsceneDropKind.None)
        {
            CancelDrag();
            message = "the zone is outside the visible area of the tree";
            return AutomationOutcome.Refused;
        }

        message = result.Accepted
            ? $"{result.Kind} row {result.ReferenceRow}: accepted, container index {result.Index}"
            : $"{result.Kind} row {result.ReferenceRow}: refused ({result.Refusal})";
        return AutomationOutcome.Done;
    }

    // The bounds of a row on the screen; false while the row has no layout. Same arithmetic as the drag rows.
    private bool TryGetRowScreenBounds(MGTreeViewItem item, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        var layout = item.HeaderContent?.LayoutBounds ?? Rectangle.Empty;
        var scrollViewer = _treeView!.ScrollViewer;
        if (layout.Height <= 0)
        {
            return false;
        }

        bounds = _treeView.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen,
            new Rectangle(layout.X - (int)scrollViewer.HorizontalOffset, layout.Y - (int)scrollViewer.VerticalOffset, layout.Width, layout.Height));
        return true;
    }
}
