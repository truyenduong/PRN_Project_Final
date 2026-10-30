using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using PRN232.Api.Contracts;
using PRN232.Domain.Exceptions;
using System.Security.Authentication;
using System.Text.Json;

namespace PRN232.Api.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    JsonSerializerOptions jsonOptions) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, code, message) = exception switch
        {
            BadRequestException ex => (StatusCodes.Status400BadRequest, "INVALID_REQUEST", ex.Message),
            InvalidRsqlException ex => (StatusCodes.Status400BadRequest, "INVALID_RSQL", ex.Message),
            UnauthorizedException ex => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", ex.Message),
            AuthenticationException => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Authentication required"),
            ForbiddenException ex => (StatusCodes.Status403Forbidden, "FORBIDDEN", ex.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "FORBIDDEN", "Access denied"),
            NotFoundException ex => (StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", ex.Message),
            ConflictException ex => (StatusCodes.Status409Conflict, "RESOURCE_ALREADY_EXISTS", ex.Message),
            DbUpdateException => (StatusCodes.Status409Conflict, "RESOURCE_ALREADY_EXISTS", "A resource with the given data already exists"),
            JsonException => (StatusCodes.Status400BadRequest, "INVALID_REQUEST", "Request body is malformed or missing"),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR", "An unexpected error occurred")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception at {Path}", httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Request failed with {StatusCode} at {Path}", statusCode, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            ApiErrorResponse.Create(code, message, httpContext.Request.Path),
            jsonOptions,
            cancellationToken);

        return true;
    }

    public static IActionResult CreateValidationResponse(ActionContext context)
    {
        var modelErrors = context.ModelState.Values.SelectMany(value => value.Errors);
        var malformedBody = modelErrors.Any(error => error.Exception is JsonException)
            || context.ModelState.TryGetValue("$", out var bodyState) && bodyState.Errors.Count > 0;

        if (malformedBody)
        {
            return new BadRequestObjectResult(ApiErrorResponse.Create(
                "INVALID_REQUEST",
                "Request body is malformed or missing",
                context.HttpContext.Request.Path));
        }

        var invalidParameter = context.ModelState.FirstOrDefault(entry =>
            entry.Value?.Errors.Any(error => IsConversionError(error.Exception)) == true);
        if (!string.IsNullOrEmpty(invalidParameter.Key))
        {
            var attemptedValue = invalidParameter.Value?.AttemptedValue ?? string.Empty;
            return new BadRequestObjectResult(ApiErrorResponse.Create(
                "INVALID_REQUEST",
                $"Invalid value '{attemptedValue}' for parameter '{invalidParameter.Key}'",
                context.HttpContext.Request.Path));
        }

        if (context.ModelState.TryGetValue("code", out var codeState)
            && codeState is not null
            && codeState.AttemptedValue is null
            && codeState.Errors.Count > 0)
        {
            return new BadRequestObjectResult(ApiErrorResponse.Create(
                "INVALID_REQUEST",
                "Required parameter 'code' is missing",
                context.HttpContext.Request.Path));
        }

        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error => new ApiFieldError(
                ToJsonFieldName(entry.Key),
                error.ErrorMessage)))
            .ToArray();

        return new BadRequestObjectResult(ApiErrorResponse.Create(
            "VALIDATION_ERROR",
            "Validation failed",
            context.HttpContext.Request.Path,
            errors));
    }

    private static bool IsConversionError(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is FormatException or OverflowException)
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }

    private static string ToJsonFieldName(string modelStateKey)
    {
        var key = modelStateKey.StartsWith("$.", StringComparison.Ordinal)
            ? modelStateKey[2..]
            : modelStateKey;

        return string.Join(
            '.',
            key.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }
}
