using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Common;
using PRN232.Domain.Exceptions;

namespace PRN232.Infrastructure.Persistence;

internal static class EfQueryOrdering
{
    public static IQueryable<TEntity> Apply<TEntity>(
        IQueryable<TEntity> query,
        IReadOnlyList<PageSort> sort,
        IReadOnlyDictionary<string, LambdaExpression> selectors)
    {
        for (var index = 0; index < sort.Count; index++)
        {
            var item = sort[index];
            if (!selectors.TryGetValue(item.Property, out var selector))
            {
                throw new InvalidOperationException($"Unsupported sort field: {item.Property}");
            }

            var method = index == 0
                ? item.Descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy)
                : item.Descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy);
            var expression = Expression.Call(
                typeof(Queryable),
                method,
                [typeof(TEntity), selector.ReturnType],
                query.Expression,
                Expression.Quote(selector));
            query = query.Provider.CreateQuery<TEntity>(expression);
        }

        return query;
    }

    public static async Task<(IReadOnlyList<TEntity> Content, long TotalElements)> PageAsync<TEntity>(
        IQueryable<TEntity> query, int page, int size, IReadOnlyList<PageSort> sort,
        IReadOnlyDictionary<string, LambdaExpression> selectors, CancellationToken cancellationToken)
    {
        if (page < 0 || size < 1)
        {
            throw new BadRequestException("Page must be non-negative and size must be positive");
        }

        var total = await query.LongCountAsync(cancellationToken);
        var ordered = Apply(query, sort, selectors);
        var content = await ordered.Skip(checked(page * size)).Take(size).ToListAsync(cancellationToken);
        return (content, total);
    }
}
