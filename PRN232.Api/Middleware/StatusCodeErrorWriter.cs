using Microsoft.AspNetCore.Diagnostics;
using PRN232.Api.Contracts;
using System.Text.Json;

namespace PRN232.Api.Middleware;

public static class StatusCodeErrorWriter
{
    public static async Task WriteAsync(StatusCodeContext context, JsonSerializerOptions jsonOptions)
    {
        var response = context.HttpContext.Response;
        response.ContentType = "application/json";

        var (code, message) = response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => ("UNAUTHORIZED", "Authentication required"),
            StatusCodes.Status403Forbidden => ("FORBIDDEN", "Access denied"),
            StatusCodes.Status404NotFound => ("RESOURCE_NOT_FOUND", "Resource not found"),
            _ => ("INTERNAL_SERVER_ERROR", "An unexpected error occurred")
        };

        await response.WriteAsJsonAsync(
            ApiErrorResponse.Create(code, message, context.HttpContext.Request.Path),
            jsonOptions,
            context.HttpContext.RequestAborted);
    }
}
