#nullable enable

using System;
using System.Collections.Generic;
using CasaEngine.Framework.Audio;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using Thickness = MonoGame.Extended.Thickness;
using HorizontalAlignment = MGUI.Core.UI.HorizontalAlignment;
using VerticalAlignment = MGUI.Core.UI.VerticalAlignment;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A dockable, read-only "Audio" panel (plan T6.2, decision P23): the statistics of the audio backend and one level meter per
/// bus of the mixer plus one for the final output. It changes nothing: the mixer stays the source of truth.
/// </summary>
/// <remarks>
/// <para>
/// The source is the single audio service of the editor (the one the play-in-editor session also uses). Call
/// <see cref="Update"/> on every editor frame while the panel is docked: the meters read every block the backend published
/// since the previous call (so a short peak is never lost, as long as the calls are less than 80 ms apart), while the texts are
/// rebuilt at most four times per second and only when a displayed value changed. The bars are drawn every frame by the
/// <see cref="AudioMeterControl"/>s from the state <see cref="Update"/> keeps.
/// </para>
/// <para>
/// Without a metering backend (anything but the software backend) the panel says so and shows the common statistics only.
/// </para>
/// </remarks>
public sealed class AudioProfilerPanel
{
    private const int NameColumnWidth = 120;
    private const int ValueColumnWidth = 52;

    private readonly MGWindow _window;
    private readonly AudioProfilerModel _model;
    private readonly MGTextBlock?[] _statBlocks = new MGTextBlock?[AudioProfilerModel.StatLineCount];
    private readonly List<MeterRow> _rows = new();

    private MGElement? _rootContent;
    private MGStackPanel? _metersHost;
    private int _builtStructureVersion = -1;
    private int _boundTextVersion = -1;

    /// <param name="window">The window the panel's controls belong to.</param>
    /// <param name="audioServiceProvider">Gives the audio service to observe, or null while there is none.</param>
    public AudioProfilerPanel(MGWindow window, Func<AudioService?> audioServiceProvider)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        ArgumentNullException.ThrowIfNull(audioServiceProvider);
        _model = new AudioProfilerModel(audioServiceProvider);
    }

    internal AudioProfilerModel Model => _model;

    /// <summary>Builds the panel once, and returns the same root afterwards. Use it as a dock panel content factory.</summary>
    public MGElement CreateContent()
    {
        if (_rootContent != null)
        {
            return _rootContent;
        }

        var stack = new MGStackPanel(_window, Orientation.Vertical)
        {
            Spacing = 2,
            Margin = new Thickness(8, 6, 8, 6),
        };

        stack.TryAddChild(new MGTextBlock(_window, "[b]Audio[/b]")
        {
            Margin = new Thickness(0, 0, 0, 2),
        });

        for (var i = 0; i < _statBlocks.Length; i++)
        {
            var block = new MGTextBlock(_window, string.Empty)
            {
                WrapText = false,
                Opacity = 0.85f,
            };

            _statBlocks[i] = block;
            stack.TryAddChild(block);
        }

        _metersHost = new MGStackPanel(_window, Orientation.Vertical)
        {
            Margin = new Thickness(0, 8, 0, 0),
        };
        stack.TryAddChild(_metersHost);

        var scrollViewer = new MGScrollViewer(_window, ScrollBarVisibility.Auto, ScrollBarVisibility.Auto);
        scrollViewer.SetContent(stack);
        _rootContent = scrollViewer;

        Update(0f);
        return _rootContent;
    }

    /// <summary>
    /// Reads the meters, rebuilds the texts if they are due, and refreshes the controls. <paramref name="elapsedSeconds"/> is the
    /// time since the previous call. Call it on every frame while the panel is docked.
    /// </summary>
    public void Update(float elapsedSeconds)
    {
        _model.Update(elapsedSeconds);

        if (_rootContent == null)
        {
            return;
        }

        if (_builtStructureVersion != _model.StructureVersion)
        {
            RebuildMeterRows();
        }

        if (_boundTextVersion != _model.TextRebuildCount)
        {
            ApplyTexts();
        }
    }

    /// <summary>Forgets the levels seen so far. Call it when the panel is docked again after it was closed.</summary>
    public void Reset()
    {
        _model.Reset();
    }

    private void RebuildMeterRows()
    {
        _builtStructureVersion = _model.StructureVersion;
        _rows.Clear();
        _metersHost!.TryRemoveAll();

        // The statistics lines already say "Metering unavailable": nothing more to show.
        if (!_model.IsMeteringAvailable || _model.Output == null)
        {
            _boundTextVersion = -1;
            return;
        }

        var grid = new MGGrid(_window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ColumnSpacing = 6,
            RowSpacing = 3,
        };
        grid.AddColumn(GridLength.CreatePixelLength(NameColumnWidth));
        grid.AddColumn(GridLength.CreateWeightedLength(1));
        grid.AddColumn(GridLength.CreatePixelLength(ValueColumnWidth));
        grid.AddColumn(GridLength.CreatePixelLength(ValueColumnWidth));
        grid.AddColumn(GridLength.CreatePixelLength(ValueColumnWidth));
        grid.AddColumn(GridLength.CreatePixelLength(ValueColumnWidth));

        int rowIndex = 0;
        grid.AddRow(GridLength.Auto);
        grid.TryAddChild(rowIndex, 0, CreateHeaderText("dBFS", HorizontalAlignment.Left));
        grid.TryAddChild(rowIndex, 2, CreateHeaderText("Peak", HorizontalAlignment.Right));
        grid.TryAddChild(rowIndex, 3, CreateHeaderText("Hold", HorizontalAlignment.Right));
        grid.TryAddChild(rowIndex, 4, CreateHeaderText("RMS", HorizontalAlignment.Right));
        grid.TryAddChild(rowIndex, 5, CreateHeaderText("Overs", HorizontalAlignment.Right));
        rowIndex++;

        var buses = _model.Buses;
        for (var i = 0; i < buses.Length; i++)
        {
            AddMeterRow(grid, rowIndex++, buses[i]);
        }

        AddMeterRow(grid, rowIndex, _model.Output);

        _metersHost.TryAddChild(grid);
        _boundTextVersion = -1;
    }

    private void AddMeterRow(MGGrid grid, int rowIndex, AudioMeterTrack track)
    {
        grid.AddRow(GridLength.Auto);

        grid.TryAddChild(rowIndex, 0, new MGTextBlock(_window, track.Name)
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(track.Depth * 10, 0, 0, 0),
            WrapText = false,
        });
        grid.TryAddChild(rowIndex, 1, new AudioMeterControl(_window, track)
        {
            MinWidth = 80,
        });

        var row = new MeterRow(
            track,
            CreateValueText(),
            CreateValueText(),
            CreateValueText(),
            CreateValueText());

        grid.TryAddChild(rowIndex, 2, row.Peak);
        grid.TryAddChild(rowIndex, 3, row.Hold);
        grid.TryAddChild(rowIndex, 4, row.Rms);
        grid.TryAddChild(rowIndex, 5, row.Overs);
        _rows.Add(row);
    }

    private MGTextBlock CreateHeaderText(string text, HorizontalAlignment alignment)
    {
        return new MGTextBlock(_window, text)
        {
            HorizontalAlignment = alignment,
            Opacity = 0.7f,
            WrapText = false,
        };
    }

    private MGTextBlock CreateValueText()
    {
        return new MGTextBlock(_window, string.Empty)
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            WrapText = false,
        };
    }

    private void ApplyTexts()
    {
        _boundTextVersion = _model.TextRebuildCount;

        var lines = _model.StatLines;
        for (var i = 0; i < _statBlocks.Length; i++)
        {
            var block = _statBlocks[i];
            if (block == null)
            {
                continue;
            }

            string line = lines[i];
            if (!string.Equals(block.Text, line, StringComparison.Ordinal))
            {
                block.Text = line;
            }

            Visibility visibility = line.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (block.Visibility != visibility)
            {
                block.Visibility = visibility;
            }
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            SetText(row.Peak, row.Track.PeakText);
            SetText(row.Hold, row.Track.HoldText);
            SetText(row.Rms, row.Track.RmsText);
            SetText(row.Overs, row.Track.OversText);
        }
    }

    private static void SetText(MGTextBlock block, string text)
    {
        if (!string.Equals(block.Text, text, StringComparison.Ordinal))
        {
            block.Text = text;
        }
    }

    private sealed class MeterRow
    {
        public MeterRow(AudioMeterTrack track, MGTextBlock peak, MGTextBlock hold, MGTextBlock rms, MGTextBlock overs)
        {
            Track = track;
            Peak = peak;
            Hold = hold;
            Rms = rms;
            Overs = overs;
        }

        public AudioMeterTrack Track { get; }

        public MGTextBlock Peak { get; }

        public MGTextBlock Hold { get; }

        public MGTextBlock Rms { get; }

        public MGTextBlock Overs { get; }
    }
}
