using PRN232.Application.Common;

namespace PRN232.Api.Controllers;

internal static class PageRequestParser
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 2_000;

    public static NormalizedPage NormalizePage(int page, int size) =>
        new(Math.Max(page, 0), size <= 0 ? DefaultPageSize : Math.Min(size, MaxPageSize));

    public static IReadOnlyList<PageSort> ParseSort(IEnumerable<string>? values) =>
        (values ?? []).Select(ParseSortValue).ToArray();

    private static PageSort ParseSortValue(string value)
    {
        var parts = value.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new InvalidOperationException($"Invalid sort parameter: {value}");
        }

        if (parts.Length == 1 || parts[1].Length == 0)
        {
            return new PageSort(parts[0], false);
        }

        if (string.Equals(parts[1], "asc", StringComparison.OrdinalIgnoreCase))
        {
            return new PageSort(parts[0], false);
        }

        if (string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase))
        {
            return new PageSort(parts[0], true);
        }

        throw new InvalidOperationException($"Invalid sort direction: {parts[1]}");
    }

    public readonly record struct NormalizedPage(int Page, int Size);
}
