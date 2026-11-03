using System.Text.Json;

namespace HireZero.Core;

// Copied unchanged from HireZero's CompanyContracts.cs: the only type the sandbox and browser adapters use from it.
public static class CompanyJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Decode<T>(string value) =>
        JsonSerializer.Deserialize<T>(value, Options) ?? throw new WorkspaceException("Invalid stored company record.");

    public static string Digest<T>(T value) => ContentGuard.Digest(Encode(value));
}
