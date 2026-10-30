using System.Text.Json;
using System.Text.Json.Serialization;

namespace PRN232.Api.Serialization;

public static class JacksonJsonOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        Configure(options);
        return options;
    }

    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = false;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
        options.Converters.Add(new LocalDateTimeJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    }
}
