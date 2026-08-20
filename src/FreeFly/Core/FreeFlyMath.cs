namespace FreeFly.Core;

internal static class FreeFlyMath
{
    public static float ApplySpeedModifiers(float baseSpeed, bool speedUp, bool slowDown, float speedUpMultiplier, float slowDownMultiplier)
    {
        if (!IsFinite(baseSpeed) || baseSpeed < 0f ||
            !IsFinite(speedUpMultiplier) || speedUpMultiplier < 0f ||
            !IsFinite(slowDownMultiplier) || slowDownMultiplier < 0f)
            return 0f;

        float multiplier = 1f;
        if (speedUp)
            multiplier *= speedUpMultiplier;
        if (slowDown)
            multiplier *= slowDownMultiplier;
        return baseSpeed * multiplier;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
