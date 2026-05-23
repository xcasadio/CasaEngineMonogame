using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.CharacterMotion;

public interface ICharacterMotionService
{
    ICharacterMotionHandle MoveTo(Entity entity, Vector3 destination, CharacterMoveToOptions options, object? owner = null);

    void Cancel(ICharacterMotionHandle handle);

    void CancelOwner(object owner);

    bool HasRequestsFor(object owner);
}