using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using PRN232.Domain.Exceptions;

namespace PRN232.Infrastructure.Persistence;

public sealed class RsqlFilter<TEntity> where TEntity : class
{
    private static readonly string[] Operators =
        ["=like=", "=out=", "=in=", "=ge=", "=gt=", "=le=", "=lt=", "!=", "==", ">=", "<=", ">", "<"];

    private readonly string _source;
    private readonly IReadOnlyDictionary<string, LambdaExpression> _selectors;
    private int _position;

    private RsqlFilter(string source, IReadOnlyDictionary<string, LambdaExpression> selectors)
    {
        _source = source;
        _selectors = selectors;
    }

    public static IQueryable<TEntity> Apply(
        IQueryable<TEntity> query, string? source, IReadOnlyDictionary<string, LambdaExpression> selectors)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return query;
        }

        try
        {
            var parser = new RsqlFilter<TEntity>(source, selectors);
            var predicate = parser.ParseOr();
            parser.SkipWhitespace();
            if (parser._position != source.Length)
            {
                throw new FormatException();
            }

            return query.Where(Expression.Lambda<Func<TEntity, bool>>(predicate, parser.RootParameter));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or
                                           InvalidOperationException or OverflowException or
                                           KeyNotFoundException or TargetInvocationException)
        {
            throw new InvalidRsqlException("Invalid RSQL query");
        }
    }

    private ParameterExpression RootParameter { get; } =
        Expression.Parameter(typeof(TEntity), "entity");

    private Expression ParseOr()
    {
        var expressions = new List<Expression> { ParseAnd() };
        while (Consume(','))
        {
            expressions.Add(ParseAnd());
        }

        return expressions.Aggregate(Expression.OrElse);
    }

    private Expression ParseAnd()
    {
        var expressions = new List<Expression> { ParseTerm() };
        while (Consume(';'))
        {
            expressions.Add(ParseTerm());
        }

        return expressions.Aggregate(Expression.AndAlso);
    }

    private Expression ParseTerm()
    {
        SkipWhitespace();
        if (Consume('('))
        {
            var nested = ParseOr();
            if (!Consume(')'))
            {
                throw new FormatException();
            }

            return nested;
        }

        return ParseComparison();
    }

    private Expression ParseComparison()
    {
        SkipWhitespace();
        var start = _position;
        while (_position < _source.Length && !char.IsWhiteSpace(_source[_position])
               && _source[_position] is not ',' and not ';' and not ')' and not '('
               && !Operators.Any(op => _source.AsSpan(_position).StartsWith(op, StringComparison.Ordinal)))
        {
            _position++;
        }

        var field = _source[start.._position];
        SkipWhitespace();
        var symbol = Operators.FirstOrDefault(op =>
            _source.AsSpan(_position).StartsWith(op, StringComparison.Ordinal));
        if (symbol is null)
        {
            throw new FormatException();
        }

        if (!_selectors.TryGetValue(field, out var selector))
        {
            throw new InvalidRsqlException($"Unknown or unsearchable field '{field}' in RSQL query");
        }

        _position += symbol.Length;
        var values = ReadValues(symbol is "=in=" or "=out=");
        return BuildPredicate(selector, symbol, values);
    }

    private Expression BuildPredicate(LambdaExpression selector, string symbol, IReadOnlyList<string> values)
    {
        var body = new ReplaceParameter(selector.Parameters[0], RootParameter).Visit(selector.Body)!;
        var valueType = Nullable.GetUnderlyingType(body.Type) ?? body.Type;
        if (symbol is "=in=" or "=out=")
        {
            var containsNull = values.Any(IsNullLiteral);
            var items = Expression.NewArrayInit(
                body.Type,
                values.Where(value => !IsNullLiteral(value))
                    .Select(value => ConvertConstant(value, valueType, body.Type)));

            var containsMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(method => method.Name == nameof(Enumerable.Contains) && method.GetParameters().Length == 2)
                    .MakeGenericMethod(body.Type);
            var contains = Expression.Call(containsMethod, items, body);
            var hasValue = Expression.Not(IsNull(body));
            if (symbol == "=in=")
            {
                return Expression.AndAlso(hasValue, contains);
            }

            return containsNull
                ? Expression.Constant(false)
                : Expression.AndAlso(hasValue, Expression.Not(contains));
        }

        if ((symbol is "==" or "!=") && IsNullLiteral(values.Single()))
        {
            var isNull = IsNull(body);
            return symbol == "==" ? isNull : Expression.Not(isNull);
        }

        var constant = ConvertConstant(values.Single(), valueType, body.Type);
        if (valueType == typeof(string)
            && (symbol == "=like=" || (symbol is "==" or "!=" && values[0].Contains('*'))))
        {
            var rawPattern = values[0];
            var pattern = symbol == "=like="
                ? (rawPattern.Contains('%') ? rawPattern : $"%{rawPattern}%")
                : rawPattern.Replace("*", "%", StringComparison.Ordinal);
            var functions = Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions))!);
            var like = Expression.Call(
                typeof(NpgsqlDbFunctionsExtensions),
                nameof(NpgsqlDbFunctionsExtensions.ILike),
                Type.EmptyTypes,
                functions,
                body,
                Expression.Constant(pattern));
            return symbol == "!="
                ? Expression.AndAlso(Expression.Not(IsNull(body)), Expression.Not(like))
                : like;
        }

        return symbol switch
        {
            "==" => Expression.Equal(body, constant),
            "!=" => Expression.AndAlso(Expression.Not(IsNull(body)), Expression.NotEqual(body, constant)),
            "=like=" => throw new FormatException(),
            ">=" or "=ge=" => Expression.GreaterThanOrEqual(body, constant),
            ">" or "=gt=" => Expression.GreaterThan(body, constant),
            "<=" or "=le=" => Expression.LessThanOrEqual(body, constant),
            "<" or "=lt=" => Expression.LessThan(body, constant),
            _ => throw new FormatException()
        };
    }

    private static Expression ConvertConstant(string value, Type valueType, Type targetType)
    {
        object parsed = valueType == typeof(Guid) ? Guid.Parse(value)
            : valueType == typeof(DateOnly) ? DateOnly.Parse(value, CultureInfo.InvariantCulture)
            : valueType == typeof(DateTime) ? DateTime.Parse(value, CultureInfo.InvariantCulture)
            : valueType.IsEnum ? Enum.Parse(valueType, value, true)
            : System.Convert.ChangeType(value, valueType, CultureInfo.InvariantCulture);

        Expression constant = Expression.Constant(parsed, valueType);
        return valueType == targetType ? constant : Expression.Convert(constant, targetType);
    }

    private static bool IsNullLiteral(string value) =>
        string.Equals(value, "null", StringComparison.OrdinalIgnoreCase);

    private static Expression IsNull(Expression expression) =>
        expression.Type.IsValueType && Nullable.GetUnderlyingType(expression.Type) is null
            ? Expression.Constant(false)
            : Expression.Equal(expression, Expression.Constant(null, expression.Type));

    private IReadOnlyList<string> ReadValues(bool list)
    {
        SkipWhitespace();
        if (list && Consume('('))
        {
            var result = new List<string>();
            do
            {
                result.Add(ReadValue());
                SkipWhitespace();
            } while (Consume(','));

            if (!Consume(')') || result.Count == 0)
            {
                throw new FormatException();
            }

            return result;
        }

        return [ReadValue()];
    }

    private string ReadValue()
    {
        SkipWhitespace();
        if (Consume('"'))
        {
            var start = _position;
            while (_position < _source.Length && _source[_position] != '"')
            {
                _position++;
            }

            if (!Consume('"'))
            {
                throw new FormatException();
            }

            return _source[start..(_position - 1)];
        }

        var valueStart = _position;
        while (_position < _source.Length && _source[_position] is not ',' and not ';' and not ')')
        {
            _position++;
        }

        var value = _source[valueStart.._position].Trim();
        if (value.Length == 0)
        {
            throw new FormatException();
        }

        return value;
    }

    private bool Consume(char value)
    {
        SkipWhitespace();
        if (_position >= _source.Length || _source[_position] != value)
        {
            return false;
        }

        _position++;
        return true;
    }

    private void SkipWhitespace()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            _position++;
        }
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
