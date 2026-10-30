using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using PRN232.Api.Contracts;
using PRN232.Api.Middleware;

namespace PRN232.Tests;

public sealed class EndpointBindingParityTests
{
    [Fact]
    public void InvalidNumericParameterUsesJavaCompatibleBadRequest()
    {
        var context = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        context.HttpContext.Request.Path = "/api/v1/tasks/abc";
        context.ModelState.SetModelValue("id", new ValueProviderResult(new StringValues("abc")));
        context.ModelState["id"]!.Errors.Add(new ModelError(new FormatException("Invalid number")));

        var result = Assert.IsType<BadRequestObjectResult>(
            GlobalExceptionHandler.CreateValidationResponse(context));
        var response = Assert.IsType<ApiErrorResponse>(result.Value);

        Assert.Equal("INVALID_REQUEST", response.Code);
        Assert.Equal("Invalid value 'abc' for parameter 'id'", response.Message);
        Assert.Equal("/api/v1/tasks/abc", response.Path);
    }

    [Fact]
    public void MissingGoogleCallbackCodeUsesJavaCompatibleBadRequest()
    {
        var context = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        context.HttpContext.Request.Path = "/api/v1/auth/google/callback";
        context.ModelState.AddModelError("code", "The code field is required.");

        var result = Assert.IsType<BadRequestObjectResult>(
            GlobalExceptionHandler.CreateValidationResponse(context));
        var response = Assert.IsType<ApiErrorResponse>(result.Value);

        Assert.Equal("INVALID_REQUEST", response.Code);
        Assert.Equal("Required parameter 'code' is missing", response.Message);
    }
}
