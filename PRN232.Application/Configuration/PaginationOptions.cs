namespace PRN232.Application.Configuration;

public sealed class PaginationOptions
{
    public const string SectionName = "Pagination";

    public int DefaultPage { get; set; } = 0;

    public int DefaultPageSize { get; set; } = 20;

    public int MaxPageSize { get; set; } = 100;

    public string DefaultSortField { get; set; } = "createdAt";
}
