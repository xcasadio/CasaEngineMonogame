using System;
using System.Collections.Generic;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class SteeringNeighborhoodFrame2D
{
    private readonly Dictionary<SteeringAgentComponent, int> _indexByAgent = [];
    private SteeringAgentComponent[] _agents = Array.Empty<SteeringAgentComponent>();
    private Entity[] _owners = Array.Empty<Entity>();
    private uint[] _participationMask = Array.Empty<uint>();
    private double[] _positionX = Array.Empty<double>();
    private double[] _positionY = Array.Empty<double>();
    private float[] _positionZ = Array.Empty<float>();
    private double[] _velocityX = Array.Empty<double>();
    private double[] _velocityY = Array.Empty<double>();
    private double[] _headingX = Array.Empty<double>();
    private double[] _headingY = Array.Empty<double>();
    private float[] _collisionRadius = Array.Empty<float>();

    public int UpdateSequence { get; private set; } = -1;

    public int Count { get; private set; }

    public SteeringAgentComponent[] Agents => _agents;

    public Entity[] Owners => _owners;

    public uint[] ParticipationMask => _participationMask;

    public double[] PositionX => _positionX;

    public double[] PositionY => _positionY;

    public float[] PositionZ => _positionZ;

    public double[] VelocityX => _velocityX;

    public double[] VelocityY => _velocityY;

    public double[] HeadingX => _headingX;

    public double[] HeadingY => _headingY;

    public float[] CollisionRadius => _collisionRadius;

    public void BeginBuild(int updateSequence, int expectedParticipantCount)
    {
        UpdateSequence = updateSequence;
        Count = 0;
        _indexByAgent.Clear();
        EnsureCapacity(expectedParticipantCount);
    }

    public int AddParticipant(
        SteeringAgentComponent agent,
        Entity owner,
        uint participationMask,
        double positionX,
        double positionY,
        float positionZ,
        double velocityX,
        double velocityY,
        double headingX,
        double headingY,
        float collisionRadius)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(owner);

        int index = Count;
        if (index >= _agents.Length)
        {
            EnsureCapacity(index + 1);
        }

        _agents[index] = agent;
        _owners[index] = owner;
        _participationMask[index] = participationMask;
        _positionX[index] = positionX;
        _positionY[index] = positionY;
        _positionZ[index] = positionZ;
        _velocityX[index] = velocityX;
        _velocityY[index] = velocityY;
        _headingX[index] = headingX;
        _headingY[index] = headingY;
        _collisionRadius[index] = collisionRadius;

        _indexByAgent[agent] = index;
        Count++;
        return index;
    }

    public bool TryGetAgentIndex(SteeringAgentComponent agent, out int index)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return _indexByAgent.TryGetValue(agent, out index);
    }

    private void EnsureCapacity(int capacity)
    {
        if (capacity <= _agents.Length)
        {
            return;
        }

        int newCapacity = Math.Max(capacity, _agents.Length == 0 ? 32 : _agents.Length * 2);
        Array.Resize(ref _agents, newCapacity);
        Array.Resize(ref _owners, newCapacity);
        Array.Resize(ref _participationMask, newCapacity);
        Array.Resize(ref _positionX, newCapacity);
        Array.Resize(ref _positionY, newCapacity);
        Array.Resize(ref _positionZ, newCapacity);
        Array.Resize(ref _velocityX, newCapacity);
        Array.Resize(ref _velocityY, newCapacity);
        Array.Resize(ref _headingX, newCapacity);
        Array.Resize(ref _headingY, newCapacity);
        Array.Resize(ref _collisionRadius, newCapacity);
    }
}