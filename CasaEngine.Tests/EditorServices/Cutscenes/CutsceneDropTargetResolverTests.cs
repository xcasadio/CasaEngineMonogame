using CasaEngine.EditorServices.Cutscenes;
using CasaEngine.Framework.Cutscenes;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

public sealed class CutsceneDropTargetResolverTests
{
    private const float RowHeight = 20f;
    private const float RowPitch = 24f; // a 4 px gap between two headers
    private const float FirstTop = 10f;

    // root Sequence [ a, p Parallel [ e, g ], q Parallel [ h ] (collapsed), r Sequence [] , b ]
    //
    // row 0  root   depth 0
    // row 1  a      depth 1
    // row 2  p      depth 1, expanded
    // row 3  e      depth 2
    // row 4  g      depth 2
    // row 5  q      depth 1, collapsed: h has no row
    // row 6  r      depth 1, empty
    // row 7  b      depth 1
    private sealed class Fixture
    {
        public readonly SequenceCutsceneActionData Root = new();
        public readonly WaitCutsceneActionData A = new() { Seconds = 1 };
        public readonly ParallelCutsceneActionData P = new();
        public readonly WaitCutsceneActionData E = new() { Seconds = 2 };
        public readonly WaitCutsceneActionData G = new() { Seconds = 3 };
        public readonly ParallelCutsceneActionData Q = new();
        public readonly WaitCutsceneActionData H = new() { Seconds = 4 };
        public readonly SequenceCutsceneActionData R = new();
        public readonly WaitCutsceneActionData B = new() { Seconds = 5 };
        public readonly List<CutsceneDropRow> Rows = new();

        public Fixture()
        {
            P.Actions.AddRange([E, G]);
            Q.Actions.Add(H);
            Root.Actions.AddRange([A, P, Q, R, B]);

            Add(Root, 0, true);
            Add(A, 1, false);
            Add(P, 1, true);
            Add(E, 2, false);
            Add(G, 2, false);
            Add(Q, 1, false);
            Add(R, 1, true);
            Add(B, 1, false);
        }

        private void Add(CutsceneActionData action, int depth, bool expanded)
        {
            int row = Rows.Count;
            Rows.Add(new CutsceneDropRow(action, FirstTop + row * RowPitch, RowHeight, depth, expanded));
        }

        public CutsceneActionData? ByName(string? name)
            => name switch
            {
                null => null,
                "root" => Root,
                "a" => A,
                "p" => P,
                "e" => E,
                "g" => G,
                "q" => Q,
                "h" => H,
                "r" => R,
                "b" => B,
                _ => throw new ArgumentException(name),
            };

        // The ordinate at a fraction of the band of a row: the band reaches the top of the next row.
        public float Y(int row, float fraction)
        {
            float top = Rows[row].Top;
            float bottom = row + 1 < Rows.Count ? Rows[row + 1].Top : top + Rows[row].Height;
            return top + (bottom - top) * fraction;
        }
    }

    private static readonly CutsceneDropArea Area = new(0f, 0f, 300f, 300f);

    private static CutsceneDropTarget Resolve(Fixture fixture, string dragged, int row, float fraction)
        => CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.ByName(dragged)!, fixture.Rows, 50f, fixture.Y(row, fraction), Area);

    [Theory]
    // Leaf: top half before, bottom half after. b is the fifth child of the root, a the first.
    [InlineData("b", 1, 0.10f, CutsceneDropKind.Before, "root", 0, 1, true)]
    [InlineData("b", 1, 0.45f, CutsceneDropKind.Before, "root", 0, 1, true)]
    [InlineData("b", 1, 0.55f, CutsceneDropKind.After, "root", 1, 1, true)]
    [InlineData("b", 1, 0.90f, CutsceneDropKind.After, "root", 1, 1, true)]
    // Leaf of the second level.
    [InlineData("b", 3, 0.10f, CutsceneDropKind.Before, "p", 0, 2, true)]
    [InlineData("b", 3, 0.90f, CutsceneDropKind.After, "p", 1, 2, true)]
    [InlineData("b", 4, 0.10f, CutsceneDropKind.Before, "p", 1, 2, true)]
    [InlineData("b", 4, 0.90f, CutsceneDropKind.After, "p", 2, 2, true)]
    // Expanded container with children: before, into (last child), after means first child.
    [InlineData("b", 2, 0.10f, CutsceneDropKind.Before, "root", 1, 1, true)]
    [InlineData("b", 2, 0.24f, CutsceneDropKind.Before, "root", 1, 1, true)]
    [InlineData("b", 2, 0.26f, CutsceneDropKind.Into, "p", 2, 1, true)]
    [InlineData("b", 2, 0.50f, CutsceneDropKind.Into, "p", 2, 1, true)]
    [InlineData("b", 2, 0.74f, CutsceneDropKind.Into, "p", 2, 1, true)]
    [InlineData("b", 2, 0.76f, CutsceneDropKind.After, "p", 0, 2, true)]
    [InlineData("b", 2, 0.90f, CutsceneDropKind.After, "p", 0, 2, true)]
    // Collapsed container that has children: after means after it, in its parent; into adds at its end.
    [InlineData("b", 5, 0.10f, CutsceneDropKind.Before, "root", 2, 1, true)]
    [InlineData("b", 5, 0.50f, CutsceneDropKind.Into, "q", 1, 1, true)]
    [InlineData("b", 5, 0.90f, CutsceneDropKind.After, "root", 3, 1, true)]
    // Empty container: into gives index 0; after means after it, in its parent.
    [InlineData("a", 6, 0.10f, CutsceneDropKind.Before, "root", 3, 1, true)]
    [InlineData("b", 6, 0.50f, CutsceneDropKind.Into, "r", 0, 1, true)]
    [InlineData("a", 6, 0.90f, CutsceneDropKind.After, "root", 4, 1, true)]
    // Root row: only into is possible; its top and bottom quarters have no place.
    [InlineData("a", 0, 0.50f, CutsceneDropKind.Into, "root", 5, 0, true)]
    [InlineData("a", 0, 0.10f, CutsceneDropKind.Before, null, -1, 0, false)]
    [InlineData("a", 0, 0.90f, CutsceneDropKind.After, null, -1, 0, false)]
    // Refused: no effect. b is already the last child of the root, a the first.
    [InlineData("b", 6, 0.90f, CutsceneDropKind.After, "root", 4, 1, false)]
    [InlineData("b", 7, 0.10f, CutsceneDropKind.Before, "root", 4, 1, false)]
    [InlineData("b", 7, 0.90f, CutsceneDropKind.After, "root", 5, 1, false)]
    [InlineData("b", 0, 0.50f, CutsceneDropKind.Into, "root", 5, 0, false)]
    [InlineData("a", 1, 0.10f, CutsceneDropKind.Before, "root", 0, 1, false)]
    [InlineData("a", 1, 0.90f, CutsceneDropKind.After, "root", 1, 1, false)]
    // Refused: a destination inside the dragged action, or the dragged action itself.
    [InlineData("p", 2, 0.50f, CutsceneDropKind.Into, "p", 2, 1, false)]
    [InlineData("p", 2, 0.90f, CutsceneDropKind.After, "p", 0, 2, false)]
    [InlineData("p", 3, 0.10f, CutsceneDropKind.Before, "p", 0, 2, false)]
    [InlineData("p", 4, 0.90f, CutsceneDropKind.After, "p", 2, 2, false)]
    // A container dropped on a place outside itself is accepted, or refused when it does not move.
    [InlineData("p", 5, 0.50f, CutsceneDropKind.Into, "q", 1, 1, true)]
    [InlineData("p", 5, 0.10f, CutsceneDropKind.Before, "root", 2, 1, false)]
    [InlineData("p", 6, 0.50f, CutsceneDropKind.Into, "r", 0, 1, true)]
    // The root cannot be dragged.
    [InlineData("root", 1, 0.10f, CutsceneDropKind.Before, "root", 0, 1, false)]
    [InlineData("root", 6, 0.50f, CutsceneDropKind.Into, "r", 0, 1, false)]
    public void EachZoneOfEachRowGivesItsDestination(string dragged, int row, float fraction, CutsceneDropKind kind, string? container, int index, int markerDepth, bool accepted)
    {
        var fixture = new Fixture();

        var target = Resolve(fixture, dragged, row, fraction);

        Assert.Equal(kind, target.Kind);
        Assert.Equal(row, target.ReferenceRow);
        Assert.Same(fixture.ByName(container), target.Container);
        Assert.Equal(index, target.Index);
        Assert.Equal(markerDepth, target.MarkerDepth);
        Assert.Equal(accepted, target.Accepted);
        Assert.Equal(accepted, target.Refusal == null);
    }

    [Fact]
    public void ARefusedDestinationOfTheSidesOfTheRootHasNoContainerAndNoMarkerToShow()
    {
        var fixture = new Fixture();

        var target = Resolve(fixture, "a", 0, 0.1f);

        Assert.False(target.Accepted);
        Assert.Null(target.Container);
        Assert.Equal(-1, target.Index);
        Assert.NotNull(target.Refusal);
    }

    [Fact]
    public void AboveTheFirstRowThereIsNoDestination()
    {
        var fixture = new Fixture();

        var target = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.B, fixture.Rows, 50f, FirstTop - 1f, Area);

        Assert.Equal(CutsceneDropKind.None, target.Kind);
        Assert.False(target.Accepted);
        Assert.Equal(-1, target.ReferenceRow);
        Assert.Null(target.Container);
        Assert.Equal(-1, target.Index);
    }

    [Fact]
    public void AnOrdinateInTheGapBetweenTwoHeadersBelongsToTheRowAbove()
    {
        var fixture = new Fixture();

        // The header of a (row 1) ends 20 px under its top; the next row starts 24 px under it.
        float inGap = fixture.Rows[1].Top + RowHeight + 2f;
        Assert.True(inGap < fixture.Rows[2].Top);

        var target = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.B, fixture.Rows, 50f, inGap, Area);

        Assert.Equal(1, target.ReferenceRow);
        Assert.Equal(CutsceneDropKind.After, target.Kind);
        Assert.Same(fixture.Root, target.Container);
        Assert.Equal(1, target.Index);
    }

    [Fact]
    public void TheGapUnderAContainerHeaderIsItsBottomQuarter()
    {
        var fixture = new Fixture();

        float inGap = fixture.Rows[2].Top + RowHeight + 2f;

        var target = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.B, fixture.Rows, 50f, inGap, Area);

        Assert.Equal(2, target.ReferenceRow);
        Assert.Equal(CutsceneDropKind.After, target.Kind);
        Assert.Same(fixture.P, target.Container);
        Assert.Equal(0, target.Index);
    }

    [Fact]
    public void UnderTheLastRowAndInTheVisibleAreaTheDestinationIsTheEndOfTheRoot()
    {
        var fixture = new Fixture();
        float underLast = fixture.Rows[7].Top + RowHeight + 5f;

        var target = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.A, fixture.Rows, 50f, underLast, Area);

        Assert.Equal(CutsceneDropKind.After, target.Kind);
        Assert.Equal(7, target.ReferenceRow);
        Assert.Same(fixture.Root, target.Container);
        Assert.Equal(5, target.Index);
        Assert.Equal(1, target.MarkerDepth);
        Assert.True(target.Accepted);
    }

    [Fact]
    public void TheEndOfTheRootIsRefusedWhenTheDraggedActionIsAlreadyLast()
    {
        var fixture = new Fixture();
        float underLast = fixture.Rows[7].Top + RowHeight + 5f;

        var target = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.B, fixture.Rows, 50f, underLast, Area);

        Assert.Equal(CutsceneDropKind.After, target.Kind);
        Assert.Same(fixture.Root, target.Container);
        Assert.Equal(5, target.Index);
        Assert.False(target.Accepted);
    }

    [Fact]
    public void TheBottomOfTheLastRowBelongsToTheEndOfTheRoot()
    {
        var fixture = new Fixture();
        float bottom = fixture.Rows[7].Top + fixture.Rows[7].Height;

        var target = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.A, fixture.Rows, 50f, bottom, Area);

        Assert.Equal(CutsceneDropKind.After, target.Kind);
        Assert.Same(fixture.Root, target.Container);
        Assert.Equal(5, target.Index);

        var insideLast = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.A, fixture.Rows, 50f, bottom - 1f, Area);

        Assert.Equal(7, insideLast.ReferenceRow);
        Assert.Equal(CutsceneDropKind.After, insideLast.Kind);
        Assert.Equal(5, insideLast.Index);
    }

    [Theory]
    [InlineData(50f, 300f)] // under the visible area
    [InlineData(50f, 400f)]
    [InlineData(300f, 100f)] // at its right
    [InlineData(500f, 100f)]
    [InlineData(-1f, 100f)] // at its left
    public void OutsideTheVisibleAreaThereIsNoDestination(float x, float y)
    {
        var fixture = new Fixture();

        var target = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.B, fixture.Rows, x, y, Area);

        Assert.Equal(CutsceneDropKind.None, target.Kind);
        Assert.False(target.Accepted);
        Assert.Null(target.Container);
    }

    [Fact]
    public void AboveTheVisibleAreaThereIsNoDestinationEvenWhenScrolledRowsAreThere()
    {
        var fixture = new Fixture();
        // The tree is scrolled: the area starts at the fourth row, the first rows are above it.
        var scrolled = new CutsceneDropArea(0f, fixture.Rows[3].Top, 300f, 300f);
        float inRowOne = fixture.Rows[1].Top + 2f;

        var above = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.B, fixture.Rows, 50f, inRowOne, scrolled);

        Assert.Equal(CutsceneDropKind.None, above.Kind);

        var inside = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.B, fixture.Rows, 50f, fixture.Y(3, 0.1f), scrolled);

        Assert.Equal(3, inside.ReferenceRow);
        Assert.Equal(CutsceneDropKind.Before, inside.Kind);
        Assert.Same(fixture.P, inside.Container);
    }

    [Fact]
    public void NoRowGivesNoDestination()
    {
        var fixture = new Fixture();

        var target = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.B, new List<CutsceneDropRow>(), 50f, 50f, Area);

        Assert.Equal(CutsceneDropKind.None, target.Kind);
    }

    [Fact]
    public void ARowThatIsNotInTheTreeGivesARefusedDestination()
    {
        var fixture = new Fixture();
        var stranger = new WaitCutsceneActionData();
        var rows = new List<CutsceneDropRow>
        {
            new(fixture.Root, FirstTop, RowHeight, 0, true),
            new(stranger, FirstTop + RowPitch, RowHeight, 1, false),
        };

        var target = CutsceneDropTargetResolver.Resolve(fixture.Root, fixture.B, rows, 50f, FirstTop + RowPitch + 2f, Area);

        Assert.Equal(CutsceneDropKind.Before, target.Kind);
        Assert.False(target.Accepted);
        Assert.Null(target.Container);
    }

    [Fact]
    public void AcceptedDestinationsMatchTheMoveCommandRefusal()
    {
        var fixture = new Fixture();
        var session = new CutsceneEditSession(new CutsceneAsset { RootAction = fixture.Root });

        for (int row = 0; row < fixture.Rows.Count; row++)
        {
            foreach (float fraction in new[] { 0.1f, 0.5f, 0.9f })
            {
                foreach (var dragged in new CutsceneActionData[] { fixture.A, fixture.P, fixture.E, fixture.B })
                {
                    var target = CutsceneDropTargetResolver.Resolve(fixture.Root, dragged, fixture.Rows, 50f, fixture.Y(row, fraction), Area);
                    if (target.Container == null)
                    {
                        continue;
                    }

                    Assert.Equal(MoveCutsceneActionCommand.GetRefusal(session, dragged, target.Container, target.Index), target.Refusal);
                }
            }
        }
    }

    [Fact]
    public void TheCalculationAllocatesNothing()
    {
        var fixture = new Fixture();
        var dragged = fixture.B;
        var rows = fixture.Rows;
        var floats = new[] { 0.1f, 0.5f, 0.9f };

        // Warm up: the first calls load the code.
        for (int row = 0; row < rows.Count; row++)
        {
            for (int fraction = 0; fraction < floats.Length; fraction++)
            {
                CutsceneDropTargetResolver.Resolve(fixture.Root, dragged, rows, 50f, fixture.Y(row, floats[fraction]), Area);
            }
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int repeat = 0; repeat < 100; repeat++)
        {
            for (int row = 0; row < rows.Count; row++)
            {
                for (int fraction = 0; fraction < floats.Length; fraction++)
                {
                    CutsceneDropTargetResolver.Resolve(fixture.Root, dragged, rows, 50f, rows[row].Top + 24f * floats[fraction], Area);
                }
            }

            CutsceneDropTargetResolver.Resolve(fixture.Root, dragged, rows, 50f, 250f, Area);
            CutsceneDropTargetResolver.Resolve(fixture.Root, dragged, rows, 500f, 250f, Area);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }
}
