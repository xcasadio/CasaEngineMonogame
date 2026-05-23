namespace CasaEngine.Framework.Scene.CharacterMotion;

public interface ICharacterMotionHandle
{
    bool IsActive { get; }

    bool HasCompleted { get; }

    bool HasReachedDestination { get; }

    bool HasTimedOut { get; }

    void Cancel();
}