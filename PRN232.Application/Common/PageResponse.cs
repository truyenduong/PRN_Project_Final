namespace PRN232.Application.Common;

public sealed record PageResponse<T>(
    IReadOnlyList<T> Content,
    int Page,
    int Size,
    long TotalElements,
    int TotalPages,
    bool First,
    bool Last);
