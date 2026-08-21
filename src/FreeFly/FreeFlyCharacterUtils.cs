using UnityEngine;

namespace FreeFly;

internal static class FreeFlyCharacterUtils
{
    public static bool IsUsable(Character character)
    {
        return character == Character.localCharacter && character.data != null &&
               character.refs.ragdoll != null && character.refs.view != null;
    }

    public static bool IsFinite(Vector3 value) =>
        FreeFly.Core.FreeFlyMath.IsFinite(value.x) &&
        FreeFly.Core.FreeFlyMath.IsFinite(value.y) &&
        FreeFly.Core.FreeFlyMath.IsFinite(value.z);
}
