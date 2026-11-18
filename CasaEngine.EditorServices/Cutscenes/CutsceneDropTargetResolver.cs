using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>Where a dragged action lands relative to the reference row.</summary>
public enum CutsceneDropKind
{
    /// <summary>No destination: the pointer is outside the visible area, or above the first row.</summary>
    None,

    /// <summary>Just before the reference row, among its siblings.</summary>
    Before,

    /// <summary>
    /// Just after the reference row among its siblings, or, for an expanded container that has children, as the first child
    /// of that container. Also the end of the root, with the last row as reference, when the pointer is under the last row.
    /// </summary>
    After,

    /// <summary>Inside the reference row, which is a container, as its last child.</summary>
    Into,
}

/// <summary>
/// A visible row of the action tree, as the drop calculation reads it: the action, where the row is and how deep it is.
/// <see cref="Top"/> and <see cref="Height"/> are in the same space as the pointer and the visible area.
/// </summary>
public readonly record struct CutsceneDropRow(CutsceneActionData Action, float Top, float Height, int Depth, bool IsExpanded);

/// <summary>The visible area of the tree, in the same space as the rows and the pointer. The right and bottom edges are outside.</summary>
public readonly record struct CutsceneDropArea(float Left, float Top, float Right, float Bottom)
{
    public bool Contains(float x, float y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

/// <summary>The result of <see cref="CutsceneDropTargetResolver.Resolve"/>.</summary>
/// <param name="Kind">The zone aimed at, or <see cref="CutsceneDropKind.None"/>.</param>
/// <param name="ReferenceRow">The index, in the row list, of the row the zone belongs to; -1 without destination.</param>
/// <param name="Container">The container that would receive the action; null without destination or for the sides of the root row.</param>
/// <param name="Index">The index among the children of <paramref name="Container"/> before the move, as in <see cref="MoveCutsceneActionCommand"/>; -1 without a container.</param>
/// <param name="MarkerDepth">The depth at which the marker of a before or after destination is drawn; the depth of the row for into.</param>
/// <param name="Refusal">Why the move is refused, or null when it is allowed.</param>
public readonly record struct CutsceneDropTarget(CutsceneDropKind Kind, int ReferenceRow, CutsceneActionData? Container, int Index, int MarkerDepth, string? Refusal)
{
    /// <summary>No destination at all.</summary>
    public static CutsceneDropTarget None => new(CutsceneDropKind.None, -1, null, -1, 0, null);

    /// <summary>True when there is a destination and the move is allowed; only then a marker is shown and a drop moves the action.</summary>
    public bool Accepted => Kind != CutsceneDropKind.None && Container != null && Refusal == null;
}

/// <summary>
/// Finds where a dragged action would drop in the tree of a cutscene (ADR-0101). A pure calculation on the visible rows: it
/// reads the list it is given and allocates nothing.
/// </summary>
/// <remarks>
/// The band of a row goes from its top to the top of the next row, so the gap between two rows belongs to the row above,
/// and the band of the last row ends at its bottom. In a band, a container splits into a top quarter (before), a middle
/// half (into) and a bottom quarter (after); a leaf splits into a top half (before) and a bottom half (after). The root row
/// only accepts into: its top and bottom quarters give a refused destination. Under the last row, inside the visible area,
/// the destination is the end of the root. Outside the visible area, or above the first row, there is none.
/// </remarks>
public static class CutsceneDropTargetResolver
{
    private const string RootHasNoSideRefusal = "The root action has no place before or after it.";
    private const string RowNotInCutsceneRefusal = "The row is not in the cutscene.";

    /// <param name="root">The root of the tree.</param>
    /// <param name="dragged">The action that is dragged.</param>
    /// <param name="rows">The visible rows, from top to bottom.</param>
    /// <param name="x">The pointer abscissa.</param>
    /// <param name="y">The pointer ordinate.</param>
    /// <param name="visibleArea">The visible area of the tree.</param>
    public static CutsceneDropTarget Resolve(CutsceneActionData root, CutsceneActionData dragged, IReadOnlyList<CutsceneDropRow> rows, float x, float y, CutsceneDropArea visibleArea)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(dragged);
        ArgumentNullException.ThrowIfNull(rows);

        int count = rows.Count;
        if (count == 0 || !visibleArea.Contains(x, y) || y < rows[0].Top)
        {
            return CutsceneDropTarget.None;
        }

        int rowIndex = 0;
        while (rowIndex + 1 < count && rows[rowIndex + 1].Top <= y)
        {
            rowIndex++;
        }

        var row = rows[rowIndex];
        bool isLast = rowIndex == count - 1;
        float bandBottom = isLast ? row.Top + row.Height : rows[rowIndex + 1].Top;
        if (y >= bandBottom)
        {
            return ResolveEndOfRoot(root, dragged, rows);
        }

        float offset = y - row.Top;
        float height = bandBottom - row.Top;
        bool isContainer = CutsceneLoadFidelity.GetChildren(row.Action) != null;
        bool isRoot = ReferenceEquals(row.Action, root);

        CutsceneDropKind kind;
        if (isContainer)
        {
            kind = offset < height * 0.25f ? CutsceneDropKind.Before
                : offset >= height * 0.75f ? CutsceneDropKind.After
                : CutsceneDropKind.Into;
        }
        else
        {
            kind = offset < height * 0.5f ? CutsceneDropKind.Before : CutsceneDropKind.After;
        }

        return kind switch
        {
            CutsceneDropKind.Into => ResolveInto(root, dragged, rowIndex, row),
            _ when isRoot => new CutsceneDropTarget(kind, rowIndex, null, -1, row.Depth, RootHasNoSideRefusal),
            CutsceneDropKind.Before => ResolveBefore(root, dragged, rowIndex, row),
            _ => ResolveAfter(root, dragged, rowIndex, row),
        };
    }

    private static CutsceneDropTarget ResolveInto(CutsceneActionData root, CutsceneActionData dragged, int rowIndex, CutsceneDropRow row)
    {
        var children = CutsceneLoadFidelity.GetChildren(row.Action)!;
        return Build(root, dragged, CutsceneDropKind.Into, rowIndex, row.Action, children.Count, row.Depth);
    }

    private static CutsceneDropTarget ResolveBefore(CutsceneActionData root, CutsceneActionData dragged, int rowIndex, CutsceneDropRow row)
    {
        if (!CutsceneActionTree.TryFindParent(root, row.Action, out var parent, out int index))
        {
            return new CutsceneDropTarget(CutsceneDropKind.Before, rowIndex, null, -1, row.Depth, RowNotInCutsceneRefusal);
        }

        return Build(root, dragged, CutsceneDropKind.Before, rowIndex, parent, index, row.Depth);
    }

    private static CutsceneDropTarget ResolveAfter(CutsceneActionData root, CutsceneActionData dragged, int rowIndex, CutsceneDropRow row)
    {
        var children = CutsceneLoadFidelity.GetChildren(row.Action);
        if (children != null && children.Count > 0 && row.IsExpanded)
        {
            return Build(root, dragged, CutsceneDropKind.After, rowIndex, row.Action, 0, row.Depth + 1);
        }

        if (!CutsceneActionTree.TryFindParent(root, row.Action, out var parent, out int index))
        {
            return new CutsceneDropTarget(CutsceneDropKind.After, rowIndex, null, -1, row.Depth, RowNotInCutsceneRefusal);
        }

        return Build(root, dragged, CutsceneDropKind.After, rowIndex, parent, index + 1, row.Depth);
    }

    private static CutsceneDropTarget ResolveEndOfRoot(CutsceneActionData root, CutsceneActionData dragged, IReadOnlyList<CutsceneDropRow> rows)
    {
        int rootDepth = 0;
        for (int index = 0; index < rows.Count; index++)
        {
            if (ReferenceEquals(rows[index].Action, root))
            {
                rootDepth = rows[index].Depth;
                break;
            }
        }

        var children = CutsceneLoadFidelity.GetChildren(root);
        if (children == null)
        {
            return new CutsceneDropTarget(CutsceneDropKind.After, rows.Count - 1, null, -1, rootDepth + 1, RootHasNoSideRefusal);
        }

        return Build(root, dragged, CutsceneDropKind.After, rows.Count - 1, root, children.Count, rootDepth + 1);
    }

    private static CutsceneDropTarget Build(CutsceneActionData root, CutsceneActionData dragged, CutsceneDropKind kind, int rowIndex, CutsceneActionData container, int index, int markerDepth)
        => new(kind, rowIndex, container, index, markerDepth, MoveCutsceneActionCommand.GetRefusal(root, dragged, container, index));
}
