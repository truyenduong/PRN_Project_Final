using PRN232.Api.Contracts;
using PRN232.Api.Serialization;
using PRN232.Application.ProjectMembers;
using System.Text.Json;

namespace PRN232.Tests;

public sealed class SerializationTests
{
    private readonly JsonSerializerOptions _options = JacksonJsonOptions.Create();

    [Fact]
    public void ErrorResponse_UsesCamelCaseAndOmitsNullErrors()
    {
        var response = ApiErrorResponse.Create(
            "RESOURCE_NOT_FOUND",
            "Project not found",
            "/api/v1/projects/42");

        var json = JsonSerializer.Serialize(response, _options);
        using var document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("RESOURCE_NOT_FOUND", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("/api/v1/projects/42", document.RootElement.GetProperty("path").GetString());
        Assert.False(document.RootElement.TryGetProperty("errors", out _));
        Assert.DoesNotContain("Z", document.RootElement.GetProperty("timestamp").GetString());
    }

    [Fact]
    public void ProjectMemberResponse_SerializesUserEmailInCamelCaseAndOmitsNull()
    {
        var response = new ProjectMemberResponse(
            1, "Project", 2, "Member", 3, "Developer", null, null)
        {
            UserEmail = "member@example.com"
        };

        var json = JsonSerializer.Serialize(response, _options);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("member@example.com", document.RootElement.GetProperty("userEmail").GetString());

        var responseWithoutEmail = response with { UserEmail = null };
        var jsonWithoutEmail = JsonSerializer.Serialize(responseWithoutEmail, _options);
        using var documentWithoutEmail = JsonDocument.Parse(jsonWithoutEmail);
        Assert.False(documentWithoutEmail.RootElement.TryGetProperty("userEmail", out _));
    }

    [Fact]
    public void EnumConverter_UsesNamesAndRejectsNumericValues()
    {
        Assert.Equal("\"SampleValue\"", JsonSerializer.Serialize(SampleEnum.SampleValue, _options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SampleEnum>("1", _options));
    }

    private enum SampleEnum
    {
        SampleValue
    }
}
