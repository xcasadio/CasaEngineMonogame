using System.Globalization;
using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

/// <summary>
/// Projects a cutscene action tree onto timeline rows and times (ADR-0098). A pure function: the tree is not modified.
/// </summary>
public static class CutsceneTimelineProjection
{
    /// <summary>Frames are converted to seconds at this rate; such items are marked as estimated.</summary>
    public const double FramesPerSecond = 60.0;

    /// <summary>The duration shown for an action whose duration only a play can measure.</summary>
    public const double UnknownDurationSeconds = 1.0;

    private const double Epsilon = 1e-6;
    private const string NoEntityLabel = "(no entity)";

    /// <summary>
    /// True when the projection shows a measured duration for this action, so that a new measure changes what it draws: the
    /// actions placed by the measured path of the builder. Other actions end without changing the layout.
    /// </summary>
    public static bool UsesMeasuredDuration(CutsceneActionData action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action is MoveToCutsceneActionData
            or NavigateToCutsceneActionData
            or PlayVideoSpriteCutsceneActionData
            or PlayVideoCutsceneActionData
            or MoveSpriteCutsceneActionData;
    }

    /// <summary>True when an action of this type gets an item; false for Sequence, Parallel and At, which have none.</summary>
    public static bool HasElement(string type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type is not (CutsceneActionTypes.Sequence or CutsceneActionTypes.Parallel or CutsceneActionTypes.At);
    }

    /// <param name="root">The root action of the cutscene; null gives an empty layout.</param>
    /// <param name="timings">The measured duration of the actions of the last play, if any.</param>
    public static CutsceneTimelineLayout Build(CutsceneActionData? root, IReadOnlyDictionary<CutsceneActionData, double>? timings = null)
    {
        if (root == null)
        {
            return CutsceneTimelineLayout.Empty;
        }

        var builder = new Builder(timings);
        double advance = builder.Place(root, 0, true);
        return builder.Finish(advance);
    }

    private sealed class Builder
    {
        private readonly IReadOnlyDictionary<CutsceneActionData, double>? _timings;
        private readonly List<CutsceneTimelineItem> _items = [];
        private readonly List<string> _entities = [];
        private double? _loopStart;
        private double? _loopEnd;

        public Builder(IReadOnlyDictionary<CutsceneActionData, double>? timings)
        {
            _timings = timings;
        }

        /// <summary>Places an action and returns how long it holds the sequence.</summary>
        public double Place(CutsceneActionData action, double start, bool topLevel)
        {
            switch (action)
            {
                case SequenceCutsceneActionData sequence:
                    return PlaceSequence(sequence.Actions, start, topLevel);

                case ParallelCutsceneActionData parallel:
                {
                    double longest = 0;
                    for (int index = 0; index < parallel.Actions.Count; index++)
                    {
                        longest = Math.Max(longest, Place(parallel.Actions[index], start, false));
                    }

                    return longest;
                }

                case RepeatCutsceneActionData repeat:
                    return PlaceRepeat(repeat, start, topLevel);

                case AtCutsceneActionData at:
                {
                    double delay = Clamp(at.Seconds);
                    return delay + PlaceSequence(at.Actions, start + delay, topLevel);
                }

                case WaitCutsceneActionData wait:
                {
                    double seconds = Clamp(wait.Seconds);
                    AddBar(action, CutsceneTimelineRowKind.Flow, null, start, seconds, false, false);
                    return seconds;
                }

                case WaitFramesCutsceneActionData waitFrames:
                {
                    double seconds = FramesToSeconds(waitFrames.Frames);
                    AddBar(action, CutsceneTimelineRowKind.Flow, null, start, seconds, true, false);
                    return seconds;
                }

                case FadeScreenCutsceneActionData fadeScreen:
                {
                    double seconds = Clamp(fadeScreen.DurationSeconds);
                    AddBar(action, CutsceneTimelineRowKind.Screen, null, start, seconds, false, false);
                    return seconds;
                }

                case FadeScreenFramesCutsceneActionData fadeFrames:
                {
                    double seconds = FramesToSeconds(fadeFrames.Frames);
                    AddBar(action, CutsceneTimelineRowKind.Screen, null, start, seconds, true, false);
                    return seconds;
                }

                case FadeMusicCutsceneActionData fadeMusic:
                {
                    double seconds = Clamp(fadeMusic.DurationSeconds);
                    AddBar(action, CutsceneTimelineRowKind.Audio, null, start, seconds, false, false);
                    return seconds;
                }

                case MoveToCutsceneActionData moveTo:
                    return PlaceMeasured(action, CutsceneTimelineRowKind.Entity, moveTo.EntityName, start, true);

                case NavigateToCutsceneActionData navigateTo:
                    return PlaceMeasured(action, CutsceneTimelineRowKind.Entity, navigateTo.EntityName, start, true);

                case PlayVideoSpriteCutsceneActionData playVideoSprite:
                    return PlaceMeasured(action, CutsceneTimelineRowKind.Entity, playVideoSprite.EntityName, start, true);

                case PlayVideoCutsceneActionData:
                    return PlaceMeasured(action, CutsceneTimelineRowKind.Screen, null, start, true);

                case MoveSpriteCutsceneActionData moveSprite:
                    PlaceMeasured(action, CutsceneTimelineRowKind.Entity, moveSprite.EntityName, start, false);
                    return 0;

                case TintSpriteCutsceneActionData tintSprite:
                    AddBar(action, CutsceneTimelineRowKind.Entity, tintSprite.EntityName, start, Clamp(tintSprite.Duration ?? 0f), false, false);
                    return 0;

                case PlayMusicCutsceneActionData playMusic:
                    AddBar(action, CutsceneTimelineRowKind.Audio, null, start, Clamp(playMusic.FadeInSeconds), false, false);
                    return 0;

                case StopMusicCutsceneActionData stopMusic:
                    AddBar(action, CutsceneTimelineRowKind.Audio, null, start, Clamp(stopMusic.FadeOutSeconds), false, false);
                    return 0;

                case ExitOnInputCutsceneActionData:
                    Add(action, CutsceneTimelineItemRole.Action, CutsceneTimelineItemShape.OpenBar, CutsceneTimelineRowKind.WorldAndInput, null, start, 0, CutsceneActionSummary.Describe(action), false, false);
                    return 0;

                case ChangeWorldCutsceneActionData:
                    AddMarker(action, CutsceneTimelineRowKind.WorldAndInput, null, start);
                    return 0;

                case OpenScreenCutsceneActionData:
                    AddMarker(action, CutsceneTimelineRowKind.Screen, null, start);
                    return 0;

                case PlaySoundCutsceneActionData:
                    AddMarker(action, CutsceneTimelineRowKind.Audio, null, start);
                    return 0;

                case ShowSpriteCutsceneActionData showSprite:
                    AddMarker(action, CutsceneTimelineRowKind.Entity, showSprite.EntityName, start);
                    return 0;

                case HideSpriteCutsceneActionData hideSprite:
                    AddMarker(action, CutsceneTimelineRowKind.Entity, hideSprite.EntityName, start);
                    return 0;

                case StartVideoSpriteCutsceneActionData startVideoSprite:
                    AddMarker(action, CutsceneTimelineRowKind.Entity, startVideoSprite.EntityName, start);
                    return 0;

                default:
                    AddMarker(action, CutsceneTimelineRowKind.Other, null, start);
                    return 0;
            }
        }

        public CutsceneTimelineLayout Finish(double advance)
        {
            double total = advance;
            for (int index = 0; index < _items.Count; index++)
            {
                total = Math.Max(total, _items[index].Shape == CutsceneTimelineItemShape.OpenBar ? _items[index].StartSeconds : _items[index].EndSeconds);
            }

            for (int index = 0; index < _items.Count; index++)
            {
                var item = _items[index];
                if (item.Shape == CutsceneTimelineItemShape.OpenBar)
                {
                    item.DurationSeconds = Math.Max(0, total - item.StartSeconds);
                }
            }

            var rows = BuildRows();
            AssignSubRows(rows);

            var ordered = new List<CutsceneTimelineItem>(_items.Count);
            AddShapes(ordered, CutsceneTimelineItemShape.Range);
            AddShapes(ordered, CutsceneTimelineItemShape.Bar, CutsceneTimelineItemShape.OpenBar);
            AddShapes(ordered, CutsceneTimelineItemShape.Marker);

            return new CutsceneTimelineLayout(rows, ordered, total, _loopStart, _loopEnd);
        }

        private double PlaceSequence(List<CutsceneActionData> actions, double start, bool topLevel)
        {
            double cursor = start;
            for (int index = 0; index < actions.Count; index++)
            {
                cursor += Place(actions[index], cursor, topLevel);
            }

            return cursor - start;
        }

        private double PlaceRepeat(RepeatCutsceneActionData repeat, double start, bool topLevel)
        {
            bool endless = repeat.Count is 0;
            int count = repeat.Count is { } value && value > 1 ? value : 1;

                int rangeIndex = _items.Count;
            double lap = PlaceSequence(repeat.Actions, start, false);

            string label = endless
                ? "Repeat forever"
                : repeat.Count == null
                    ? "Repeat ×?"
                    : $"Repeat ×{count.ToString(CultureInfo.InvariantCulture)}";
            _items.Insert(rangeIndex, MakeRange(repeat, CutsceneTimelineItemRole.RepeatLap, start, lap, label));

            if (endless)
            {
                if (topLevel && _loopStart == null)
                {
                    _loopStart = start;
                    _loopEnd = start + lap;
                }

                return lap;
            }

            if (count > 1)
            {
                string more = $"×{(count - 1).ToString(CultureInfo.InvariantCulture)} more";
                _items.Insert(rangeIndex + 1, MakeRange(repeat, CutsceneTimelineItemRole.RepeatMoreLaps, start + lap, lap * (count - 1), more));
            }

            return lap * count;
        }

        private static CutsceneTimelineItem MakeRange(RepeatCutsceneActionData repeat, CutsceneTimelineItemRole role, double start, double duration, string label)
        {
            return new CutsceneTimelineItem(repeat, role, CutsceneTimelineItemShape.Range, start, duration, label, false, false)
            {
                RowKind = CutsceneTimelineRowKind.Loops,
            };
        }

        private double PlaceMeasured(CutsceneActionData action, CutsceneTimelineRowKind kind, string? entity, double start, bool advances)
        {
            bool measured = _timings != null && _timings.TryGetValue(action, out _);
            double seconds = measured ? Clamp(_timings![action]) : UnknownDurationSeconds;
            AddBar(action, kind, entity, start, seconds, !measured, measured);
            return advances ? seconds : 0;
        }

        private void AddBar(CutsceneActionData action, CutsceneTimelineRowKind kind, string? entity, double start, double seconds, bool estimated, bool measured)
        {
            var shape = seconds > 0 ? CutsceneTimelineItemShape.Bar : CutsceneTimelineItemShape.Marker;
            Add(action, CutsceneTimelineItemRole.Action, shape, kind, entity, start, seconds, CutsceneActionSummary.Describe(action), estimated, measured);
        }

        private void AddMarker(CutsceneActionData action, CutsceneTimelineRowKind kind, string? entity, double start)
        {
            Add(action, CutsceneTimelineItemRole.Action, CutsceneTimelineItemShape.Marker, kind, entity, start, 0, CutsceneActionSummary.Describe(action), false, false);
        }

        private void Add(
            CutsceneActionData action,
            CutsceneTimelineItemRole role,
            CutsceneTimelineItemShape shape,
            CutsceneTimelineRowKind kind,
            string? entity,
            double start,
            double duration,
            string label,
            bool estimated,
            bool measured)
        {
            string? key = null;
            if (kind == CutsceneTimelineRowKind.Entity)
            {
                key = string.IsNullOrWhiteSpace(entity) ? string.Empty : entity;
                if (!_entities.Contains(key))
                {
                    _entities.Add(key);
                }
            }

            _items.Add(new CutsceneTimelineItem(action, role, shape, start, duration, label, estimated, measured)
            {
                RowKind = kind,
                EntityKey = key,
            });
        }

        private List<CutsceneTimelineRow> BuildRows()
        {
            var rows = new List<CutsceneTimelineRow>();
            AddRowIfUsed(rows, CutsceneTimelineRowKind.Loops, "Loops");
            AddRowIfUsed(rows, CutsceneTimelineRowKind.Flow, "Flow");
            if (HasKind(CutsceneTimelineRowKind.Entity))
            {
                for (int index = 0; index < _entities.Count; index++)
                {
                    string name = _entities[index];
                    rows.Add(new CutsceneTimelineRow(CutsceneTimelineRowKind.Entity, name.Length == 0 ? NoEntityLabel : name, name));
                }
            }

            AddRowIfUsed(rows, CutsceneTimelineRowKind.Screen, "Screen");
            AddRowIfUsed(rows, CutsceneTimelineRowKind.Audio, "Audio");
            AddRowIfUsed(rows, CutsceneTimelineRowKind.WorldAndInput, "World and input");
            AddRowIfUsed(rows, CutsceneTimelineRowKind.Other, "Other");

            for (int index = 0; index < _items.Count; index++)
            {
                var item = _items[index];
                for (int row = 0; row < rows.Count; row++)
                {
                    if (rows[row].Kind == item.RowKind
                        && (item.RowKind != CutsceneTimelineRowKind.Entity || rows[row].EntityName == item.EntityKey))
                    {
                        item.RowIndex = row;
                        break;
                    }
                }
            }

            return rows;
        }

        private void AddRowIfUsed(List<CutsceneTimelineRow> rows, CutsceneTimelineRowKind kind, string label)
        {
            if (HasKind(kind))
            {
                rows.Add(new CutsceneTimelineRow(kind, label, null));
            }
        }

        private bool HasKind(CutsceneTimelineRowKind kind)
        {
            for (int index = 0; index < _items.Count; index++)
            {
                if (_items[index].RowKind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        // Overlapping bars and ranges of a row go to separate sub-rows; a marker stays on the first one.
        private void AssignSubRows(List<CutsceneTimelineRow> rows)
        {
            var spans = new List<CutsceneTimelineItem>();
            var ends = new List<double>();
            for (int row = 0; row < rows.Count; row++)
            {
                spans.Clear();
                ends.Clear();
                for (int index = 0; index < _items.Count; index++)
                {
                    var item = _items[index];
                    if (item.RowIndex == row && item.Shape != CutsceneTimelineItemShape.Marker)
                    {
                        spans.Add(item);
                    }
                }

                // Stable order by start: the list is in tree order, and insertion sort keeps ties in that order.
                for (int i = 1; i < spans.Count; i++)
                {
                    var current = spans[i];
                    int j = i - 1;
                    while (j >= 0 && spans[j].StartSeconds > current.StartSeconds)
                    {
                        spans[j + 1] = spans[j];
                        j--;
                    }

                    spans[j + 1] = current;
                }

                for (int i = 0; i < spans.Count; i++)
                {
                    var item = spans[i];
                    int sub = 0;
                    while (sub < ends.Count && ends[sub] > item.StartSeconds + Epsilon)
                    {
                        sub++;
                    }

                    if (sub == ends.Count)
                    {
                        ends.Add(0);
                    }

                    ends[sub] = item.EndSeconds;
                    item.SubRow = sub;
                }

                rows[row].SubRowCount = Math.Max(1, ends.Count);
            }
        }

        private void AddShapes(List<CutsceneTimelineItem> target, CutsceneTimelineItemShape first, CutsceneTimelineItemShape? second = null)
        {
            for (int index = 0; index < _items.Count; index++)
            {
                var shape = _items[index].Shape;
                if (shape == first || shape == second)
                {
                    target.Add(_items[index]);
                }
            }
        }

        private static double FramesToSeconds(int? frames) => frames is { } count && count > 0 ? count / FramesPerSecond : 0;

        private static double Clamp(float value) => float.IsFinite(value) && value > 0 ? value : 0;

        private static double Clamp(double value) => double.IsFinite(value) && value > 0 ? value : 0;
    }
}
