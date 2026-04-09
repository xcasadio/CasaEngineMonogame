namespace CasaEngine.Framework.AI.Navigation;

public interface ISteeringPreciseKinematicsProvider
{
    SteeringForceVector SteeringPrecisePosition { get; }

    SteeringForceVector SteeringPreciseVelocity { get; }

    SteeringForceVector SteeringPreciseHeading { get; }
}