using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class NavigationAgentComponent : EntityComponent, IWorldSystemDrivenComponent
{
    private CharacterControllerNavigationDriverComponent? _driver;
    private bool _needsPath;

    public NavigationGrid2D? NavigationMap { get; set; }

    public NavigationQuery Query { get; set; } = new();

    public Vector3 Destination { get; private set; }

    public NavigationPath? CurrentPath { get; private set; }

    public float StoppingDistance { get; set; } = 0.1f;

    public bool AutoRequestPathOnUpdate { get; set; } = true;

    public bool HasDestination { get; private set; }

    public bool HasPath { get; private set; }

    public bool ReachedDestination { get; private set; }

    public bool IsPathRequestPending => _needsPath;

    public override void Attach(Entity actor)
    {
        base.Attach(actor);
        ResolveDriver();
    }

    public override void InitializeWithWorld(Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        ResolveDriver();
    }

    public void MoveTo(Vector3 destination)
    {
        Destination = destination;
        CurrentPath = null;
        HasDestination = true;
        HasPath = false;
        ReachedDestination = false;
        _needsPath = true;
    }

    public void Cancel()
    {
        _needsPath = false;
        HasDestination = false;
        HasPath = false;
        ReachedDestination = false;
        CurrentPath = null;
        ResolveDriver();
        _driver?.Cancel();
    }

    public bool RequestPath()
    {
        _needsPath = false;
        ResolveDriver();

        if (!HasDestination || NavigationMap == null || Owner?.RootComponent == null)
        {
            HasPath = false;
            CurrentPath = null;
            return false;
        }

        if (!NavigationMap.TryFindPath(Owner.RootComponent.Position, Destination, Query, out NavigationPath? path) || path == null)
        {
            HasPath = false;
            CurrentPath = null;
            ReachedDestination = false;
            return false;
        }

        CurrentPath = path;
        HasPath = true;
        ReachedDestination = false;

        if (_driver != null)
        {
            _driver.SetPath(path.Points, StoppingDistance);
        }

        return true;
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (AutoRequestPathOnUpdate && _needsPath)
        {
            RequestPath();
        }

        ResolveDriver();
        if (HasPath && _driver != null && !_driver.IsMoving && _driver.HasReachedDestination)
        {
            HasPath = false;
            HasDestination = false;
            ReachedDestination = true;
        }
    }

    public override EntityComponent Clone()
    {
        return new NavigationAgentComponent
        {
            NavigationMap = NavigationMap,
            Query = Query.Clone(),
            Destination = Destination,
            CurrentPath = CurrentPath,
            StoppingDistance = StoppingDistance,
            AutoRequestPathOnUpdate = AutoRequestPathOnUpdate,
            HasDestination = HasDestination,
            HasPath = HasPath,
            ReachedDestination = ReachedDestination,
            _needsPath = _needsPath,
        };
    }

    private void ResolveDriver()
    {
        _driver = Owner?.GetComponent<CharacterControllerNavigationDriverComponent>();
    }
}