using System;
using System.Collections.Generic;
using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Editor.Styling;
using CasaEngine.Framework.Cutscenes;
using MGUI.Core.UI;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Mouse;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Drag and drop of an action inside the Hierarchy tree (ADR-0101, P5 to P7). MGUI is not changed: the tree listens to the
/// drag of the left button, finds the row under the pointer from the bounds of the row headers, asks
/// <see cref="CutsceneDropTargetResolver"/> where the action would land and draws the marker itself.
/// </summary>
/// <remarks>
/// The press of a row only reaches the mouse handler of the tree, not a handler of the header of the row, and only once the
/// drag is marked as handled: otherwise the window takes the drag and the later events never come. The header of a row is as
/// tall as the row, so its bounds are the row. The layout bounds of a header are in the space of the content of the scroll
/// viewer: they do not move when the tree scrolls, and the offset of the scroll viewer is taken off to get the screen. The
/// pointer, the rows and the visible area are all worked out in screen space. Hot paths (drag moves, marker drawing, drop
/// lookup) allocate nothing: the structure of the visible rows is kept and rebuilt only when the tree changes, and the top
/// and height of every row are read again, in place, at each use.
/// </remarks>
internal sealed partial class CutsceneOutlinerPanel
{
    private static readonly Color MarkerColor = EditorThemePalette.InlineRenameBorder;
    private static readonly Color MarkerFill = EditorThemePalette.DropHighlight;

    private const float MarkerThickness = 2f;

    /// <summary>A row of the tree that is shown: its element, its action, how deep it is and whether it was expanded when listed.</summary>
    private readonly record struct VisibleRow(MGTreeViewItem Item, CutsceneActionData Action, int Depth, bool IsExpanded);

    private readonly List<VisibleRow> _visibleRows = new();
    private readonly List<CutsceneDropRow> _dropRows = new();
    private readonly List<float> _rowLefts = new();
    private bool _visibleRowsDirty = true;

    private CutsceneActionData? _dragAction;
    private CutsceneDropTarget _dragTarget = CutsceneDropTarget.None;
    // The pointer of the drag, in screen space.
    private Vector2 _dragPointer;

    /// <summary>The action that is dragged, or null when no drag is in progress.</summary>
    internal CutsceneActionData? DragAction => _dragAction;

    /// <summary>Where the dragged action would land, as of the last pointer move or marker drawing; <see cref="CutsceneDropTarget.None"/> without a drag.</summary>
    internal CutsceneDropTarget DragTarget => _dragTarget;

    internal bool IsDragging => _dragAction != null;

    private void AttachDrag(MGTreeView treeView)
    {
        treeView.MouseHandler.DragStart += OnTreeDragStart;
        treeView.MouseHandler.Dragged += OnTreeDragged;
        treeView.MouseHandler.DragEnd += OnTreeDragEnd;
        treeView.KeyboardHandler.Pressed += OnTreeKeyPressed;
        treeView.OnEndDraw += OnTreeEndDraw;
    }

    /// <summary>Ends the drag in progress, if any: the action, the destination and the marker are cleared and the events that follow are ignored.</summary>
    private void CancelDrag()
    {
        _dragAction = null;
        _dragTarget = CutsceneDropTarget.None;
    }

    private void OnTreeDragStart(object? sender, BaseMouseDragStartEventArgs e)
    {
        CancelDrag();
        var root = _document?.Session?.Asset.RootAction;
        if (_treeView == null || root == null || !e.IsLMB || !TryFillDropRows(out var area))
        {
            return;
        }

        var pointer = new Vector2(e.Position.X, e.Position.Y);
        if (!area.Contains(pointer.X, pointer.Y))
        {
            return;
        }

        for (int index = 0; index < _visibleRows.Count; index++)
        {
            var row = _dropRows[index];
            if (pointer.Y < row.Top || pointer.Y >= row.Top + row.Height)
            {
                continue;
            }

            // The expander of a row is not a handle, and the root has no place to move to.
            var action = _visibleRows[index].Action;
            if (pointer.X < _rowLefts[index] || ReferenceEquals(action, root))
            {
                return;
            }

            _dragAction = action;
            _dragPointer = pointer;
            // The drag is ours: without this the window takes it and the tree never sees the moves and the release.
            e.SetHandledBy(_treeView, false);
            return;
        }
    }

    private void OnTreeDragged(object? sender, BaseMouseDraggedEventArgs e)
    {
        if (_dragAction == null)
        {
            return;
        }

        _dragPointer = new Vector2(e.Position.X, e.Position.Y);
        RefreshDragTarget();
    }

    private void OnTreeDragEnd(object? sender, BaseMouseDragEndEventArgs e)
    {
        var action = _dragAction;
        if (action == null || _document == null)
        {
            return;
        }

        // The destination is worked out again from where the pointer is released, not taken from the last move.
        _dragPointer = new Vector2(e.EndPosition.X, e.EndPosition.Y);
        RefreshDragTarget();
        var target = _dragTarget;

        // The move rebuilds the tree, which would cancel the drag: it is over before the document changes.
        CancelDrag();
        if (target.Accepted)
        {
            ShowRefusal(_document.MoveAction(action, target.Container!, target.Index));
        }
    }

    private void OnTreeKeyPressed(object? sender, BaseKeyPressedEventArgs e)
    {
        if (e.IsHandled)
        {
            return;
        }

        // The two keys the panel takes (ADR-0101, P6 and P11); the arrows and the rest belong to the tree. The event comes only
        // while the tree has the keyboard focus, so Delete never removes an action while the user works elsewhere.
        if (e.Key == Keys.Escape && _dragAction != null)
        {
            CancelDrag();
            e.SetHandledBy(_treeView, true);
        }
        else if (e.Key == Keys.Delete && _document != null && _dragAction == null)
        {
            DeleteSelected();
            e.SetHandledBy(_treeView, true);
        }
    }

    /// <summary>Works out again where the dragged action would land from the pointer of the drag and the rows as they are now; allocates nothing.</summary>
    internal CutsceneDropTarget RefreshDragTarget()
    {
        var root = _document?.Session?.Asset.RootAction;
        if (_dragAction == null || root == null || !TryFillDropRows(out var area))
        {
            _dragTarget = CutsceneDropTarget.None;
            return _dragTarget;
        }

        _dragTarget = CutsceneDropTargetResolver.Resolve(root, _dragAction, _dropRows, _dragPointer.X, _dragPointer.Y, area);
        return _dragTarget;
    }

    // Lists the rows that are shown. Their element, action and depth change only when the tree is rebuilt or a row is expanded
    // or collapsed; a row whose expansion differs from the listing shows that the listing is stale.
    private void EnsureVisibleRows()
    {
        if (!_visibleRowsDirty)
        {
            bool stale = false;
            for (int index = 0; index < _visibleRows.Count; index++)
            {
                if (_visibleRows[index].Item.IsExpanded != _visibleRows[index].IsExpanded)
                {
                    stale = true;
                    break;
                }
            }

            if (!stale)
            {
                return;
            }
        }

        _visibleRows.Clear();
        var items = _treeView!.Items;
        for (int index = 0; index < items.Count; index++)
        {
            AddVisibleRows(items[index], 0);
        }

        _visibleRowsDirty = false;
    }

    private void AddVisibleRows(MGTreeViewItem item, int depth)
    {
        if (!_itemToAction.TryGetValue(item, out var action))
        {
            return;
        }

        bool expanded = item.IsExpanded;
        _visibleRows.Add(new VisibleRow(item, action, depth, expanded));
        if (!expanded)
        {
            return;
        }

        var children = item.Items;
        for (int index = 0; index < children.Count; index++)
        {
            AddVisibleRows(children[index], depth + 1);
        }
    }

    // Fills _dropRows and _rowLefts, in the order of _visibleRows, with the top, height and left of every row on the screen as
    // they are now, and gives the visible area of the tree on the screen. False while the tree has no layout yet: there is
    // then nothing to aim at.
    private bool TryFillDropRows(out CutsceneDropArea area)
    {
        area = default;
        var treeView = _treeView;
        if (treeView?.ScrollViewer == null)
        {
            return false;
        }

        EnsureVisibleRows();
        _dropRows.Clear();
        _rowLefts.Clear();
        var scrollViewer = treeView.ScrollViewer;
        int scrollX = (int)scrollViewer.HorizontalOffset;
        int scrollY = (int)scrollViewer.VerticalOffset;
        for (int index = 0; index < _visibleRows.Count; index++)
        {
            var row = _visibleRows[index];
            var layout = row.Item.HeaderContent?.LayoutBounds ?? Rectangle.Empty;
            if (layout.Height <= 0)
            {
                _dropRows.Clear();
                _rowLefts.Clear();
                return false;
            }

            var screen = treeView.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen,
                new Rectangle(layout.X - scrollX, layout.Y - scrollY, layout.Width, layout.Height));
            _dropRows.Add(new CutsceneDropRow(row.Action, screen.Top, screen.Height, row.Depth, row.IsExpanded));
            _rowLefts.Add(screen.Left);
        }

        var viewport = treeView.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, scrollViewer.ContentViewport);
        area = new CutsceneDropArea(viewport.Left, viewport.Top, viewport.Right, viewport.Bottom);
        return _dropRows.Count > 0 && viewport.Width > 0 && viewport.Height > 0;
    }

    // The marker is drawn after the clipping of the tree is restored, so it is cut to the visible area of the tree here. It is
    // worked out on the screen and drawn in the layout space of the tree.
    private void OnTreeEndDraw(object? sender, MGElement.MGElementDrawEventArgs e)
    {
        if (_dragAction == null || _treeView == null)
        {
            return;
        }

        var target = RefreshDragTarget();
        if (!target.Accepted || target.ReferenceRow < 0 || target.ReferenceRow >= _dropRows.Count)
        {
            return;
        }

        var viewport = _treeView.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, _treeView.ScrollViewer.ContentViewport);
        var row = _dropRows[target.ReferenceRow];
        var da = e.DA;
        var origin = da.Offset.ToVector2();

        if (target.Kind == CutsceneDropKind.Into)
        {
            // A frame around the row of the container.
            float left = viewport.Left;
            float width = viewport.Width;
            float top = row.Top;
            float height = row.Height;
            FillClipped(da, origin, viewport, left, top, width, height, MarkerFill);
            FillClipped(da, origin, viewport, left, top, width, MarkerThickness, MarkerColor);
            FillClipped(da, origin, viewport, left, top + height - MarkerThickness, width, MarkerThickness, MarkerColor);
            FillClipped(da, origin, viewport, left, top, MarkerThickness, height, MarkerColor);
            FillClipped(da, origin, viewport, left + width - MarkerThickness, top, MarkerThickness, height, MarkerColor);
            return;
        }

        // A line between two rows, at the indentation of the destination.
        float lineY = target.Kind == CutsceneDropKind.Before ? row.Top : row.Top + row.Height;
        float lineLeft = _rowLefts[target.ReferenceRow] + ((target.MarkerDepth - row.Depth) * _treeView.IndentSize);
        FillClipped(da, origin, viewport, lineLeft, lineY - (MarkerThickness / 2f), viewport.Right - lineLeft, MarkerThickness, MarkerColor);
    }

    // Fills a rectangle given on the screen, cut to the visible area of the tree (also on the screen).
    private void FillClipped(ElementDrawArgs da, Vector2 origin, Rectangle viewport, float x, float y, float width, float height, Color color)
    {
        float left = Math.Max(x, viewport.Left);
        float top = Math.Max(y, viewport.Top);
        float right = Math.Min(x + width, viewport.Right);
        float bottom = Math.Min(y + height, viewport.Bottom);
        if (right <= left || bottom <= top)
        {
            return;
        }

        var topLeft = _treeView!.ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, new Vector2(left, top));
        da.Context.FillRectangle(origin, new RectangleF(topLeft.X, topLeft.Y, right - left, bottom - top), color * da.Opacity);
    }
}
