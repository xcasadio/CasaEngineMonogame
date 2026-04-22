using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public readonly record struct SkeletonJointDefinition(
    string Name,
    int ParentIndex,
    BoneTransform LocalBindTransform,
    Matrix InverseBindMatrix,
    int SkinPaletteIndex = -1);