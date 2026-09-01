namespace FreeFly.Core;

internal static class FreeFlyInputRules
{
    public static string NormalizeBindingPath(string? path)
    {
        return path?.Trim() ?? string.Empty;
    }

    public static int ClampSelection(int selectedIndex, int optionCount)
    {
        if (optionCount <= 0)
            return 0;
        return selectedIndex < 0 ? 0 : selectedIndex >= optionCount ? optionCount - 1 : selectedIndex;
    }
}
