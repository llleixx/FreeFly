namespace FreeFly.Core;

internal static class FreeFlyInputRules
{
    public static string NormalizeBindingPath(string? path)
    {
        string normalized = path?.Trim() ?? string.Empty;
        return string.Equals(normalized, "None", System.StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : normalized;
    }

    public static int ClampSelection(int selectedIndex, int optionCount)
    {
        if (optionCount <= 0)
            return 0;
        return selectedIndex < 0 ? 0 : selectedIndex >= optionCount ? optionCount - 1 : selectedIndex;
    }
}
