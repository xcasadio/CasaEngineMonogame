using System;
using System.Collections.Generic;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class SteeringNeighborhoodCellGrid2D
{
    private readonly Dictionary<long, int> _cellIndexByKey = [];
    private int[] _cellStartIndices = Array.Empty<int>();
    private int[] _cellCounts = Array.Empty<int>();
    private int[] _cellWriteCursor = Array.Empty<int>();
    private int[] _packedParticipantIndices = Array.Empty<int>();

    public float CellSize { get; private set; }

    public int ActiveCellCount { get; private set; }

    public double AverageOccupancy { get; private set; }

    public int MaxCellOccupancy { get; private set; }

    public int[] PackedParticipantIndices => _packedParticipantIndices;

    public void Build(SteeringNeighborhoodFrame2D frame, float cellSize)
    {
        ArgumentNullException.ThrowIfNull(frame);

        CellSize = cellSize > 1.0f ? cellSize : 1.0f;
        ActiveCellCount = 0;
        AverageOccupancy = 0.0;
        MaxCellOccupancy = 0;
        _cellIndexByKey.Clear();

        EnsureCapacity(frame.Count);
        Array.Clear(_cellCounts, 0, _cellCounts.Length);

        for (int participantIndex = 0; participantIndex < frame.Count; participantIndex++)
        {
            long cellKey = CombineCellKey(ToCell(frame.PositionX[participantIndex]), ToCell(frame.PositionY[participantIndex]));
            if (!_cellIndexByKey.TryGetValue(cellKey, out int cellIndex))
            {
                cellIndex = ActiveCellCount;
                _cellIndexByKey.Add(cellKey, cellIndex);
                _cellCounts[cellIndex] = 0;
                ActiveCellCount++;
            }

            _cellCounts[cellIndex]++;
            if (_cellCounts[cellIndex] > MaxCellOccupancy)
            {
                MaxCellOccupancy = _cellCounts[cellIndex];
            }
        }

        int runningIndex = 0;
        for (int cellIndex = 0; cellIndex < ActiveCellCount; cellIndex++)
        {
            _cellStartIndices[cellIndex] = runningIndex;
            _cellWriteCursor[cellIndex] = runningIndex;
            runningIndex += _cellCounts[cellIndex];
        }

        for (int participantIndex = 0; participantIndex < frame.Count; participantIndex++)
        {
            long cellKey = CombineCellKey(ToCell(frame.PositionX[participantIndex]), ToCell(frame.PositionY[participantIndex]));
            int cellIndex = _cellIndexByKey[cellKey];
            _packedParticipantIndices[_cellWriteCursor[cellIndex]++] = participantIndex;
        }

        AverageOccupancy = ActiveCellCount > 0
            ? (double)frame.Count / ActiveCellCount
            : 0.0;
    }

    public bool TryGetCellRange(int cellX, int cellY, out int startIndex, out int count)
    {
        if (_cellIndexByKey.TryGetValue(CombineCellKey(cellX, cellY), out int cellIndex))
        {
            startIndex = _cellStartIndices[cellIndex];
            count = _cellCounts[cellIndex];
            return true;
        }

        startIndex = 0;
        count = 0;
        return false;
    }

    public int ToCell(double value)
    {
        return (int)Math.Floor(value / CellSize);
    }

    private void EnsureCapacity(int participantCount)
    {
        int requiredCellCapacity = Math.Max(participantCount, 1);
        if (_cellStartIndices.Length < requiredCellCapacity)
        {
            int newCapacity = Math.Max(requiredCellCapacity, _cellStartIndices.Length == 0 ? 32 : _cellStartIndices.Length * 2);
            Array.Resize(ref _cellStartIndices, newCapacity);
            Array.Resize(ref _cellCounts, newCapacity);
            Array.Resize(ref _cellWriteCursor, newCapacity);
        }

        if (_packedParticipantIndices.Length < participantCount)
        {
            int newCapacity = Math.Max(participantCount, _packedParticipantIndices.Length == 0 ? 32 : _packedParticipantIndices.Length * 2);
            Array.Resize(ref _packedParticipantIndices, newCapacity);
        }
    }

    private static long CombineCellKey(int cellX, int cellY)
    {
        return ((long)cellX << 32) ^ (uint)cellY;
    }
}