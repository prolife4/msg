using System.Text.Json.Serialization;

namespace Vmx.Messenger.Services.MAX;

internal sealed class MaxUploadResponse
{
    [JsonPropertyName("photos")]
    public Dictionary<string, MaxUploadPhoto>? Photos { get; set; }

    public string? GetFirstToken() => Photos?.Values.FirstOrDefault()?.Token;
}

internal sealed class MaxUploadPhoto
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}